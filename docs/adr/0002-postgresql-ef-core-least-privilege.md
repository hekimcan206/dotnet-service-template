# 0002 — PostgreSQL via EF Core, migrations by a separate least-privilege process

- Status: accepted
- Date: 2026-11-06

## Context

The service needs a relational store, schema migrations, and protection against lost updates when
two requests modify the same order. The account the API connects with is a security boundary. If the
web process can run DDL, then an SQL injection or a bad deploy can drop tables.

## Options considered

**Data access**

1. **Dapper + hand-written SQL.** Fast, explicit and great for read models. Change tracking,
   concurrency tokens and migrations would all have to be written by hand.
2. **EF Core 10 + Npgsql.** Change tracking fits aggregate persistence well. The owned collection
   maps `Order.Lines` to its own table without exposing setters. `xmin` gives optimistic concurrency
   without adding a column, and migrations are generated from the model.

**When to migrate**

1. **`Database.Migrate()` at every API start-up.** Simple, but every replica races to migrate (EF 9+
   takes a lock, but start-up still blocks on it), and the runtime account must own the schema.
2. **Idempotent SQL script or migration bundle run by CI/CD.** The most control, but `docker compose up`
   would need an extra pipeline step.
3. **The same image with `--migrate-only`, run as a one-shot job with the schema-owner role, before the
   API starts.** It works in compose (`service_completed_successfully`), as a Kubernetes Job or init
   container, or as a pipeline step.

## Decision

- EF Core with Npgsql, with snake_case names mapped explicitly and a dedicated `service` schema.
- Two roles, created by `deploy/postgres/init/01-roles.sh`:
  - `service_migrator` owns the schema and runs migrations.
  - `service_app` has `USAGE` on the schema and `SELECT/INSERT/UPDATE/DELETE` on its tables through
    `ALTER DEFAULT PRIVILEGES`. It cannot `CREATE`, `DROP` or `ALTER`.
- Migrations run through `--migrate-only` (option 3). `Database:MigrateOnStartup` exists only for
  local `dotnet run` against the compose database.
- Concurrency uses `xmin` as the row version. Every mutation also updates `orders.updated_at`, so a
  change that only touches `order_lines` still bumps the order row and is checked. Lost races, whether
  a stale `xmin` or a duplicate insert (unique violation), come back as `409 Conflict`.
- The database repeats the key invariants (check constraints on status, quantity and price) as a last
  line of defence.

## Consequences

- An integration test (`Runtime_role_has_dml_rights_only`) uses the same init script as compose and
  proves the API role gets `42501 insufficient_privilege` on DDL.
- `Concurrent_additions_never_lose_an_update` fires 20 parallel writes at one order and checks that
  the final quantity equals the number of `200` responses.
- No retrying execution strategy is configured: a retry wrapped around a non-idempotent POST can apply
  it twice. Add retries per operation, with idempotency keys, when a service needs them.
- The init script runs only when the data volume is first created. Changing role passwords later
  means `docker compose down -v` or an explicit `ALTER ROLE`.
