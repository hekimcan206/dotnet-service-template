# ServiceTemplate

> Generated from [hekim-service](https://github.com/hekimcan206/dotnet-service-template). Replace this paragraph with
> one sentence on the problem this service solves and who calls it.

## Quick start

```bash
docker compose up --build          # postgres + migrator + api
curl http://localhost:58080/health/ready
open http://localhost:58080/scalar  # interactive API reference
```

Local development against the compose database:

```bash
docker compose up -d postgres
dotnet run --project src/ServiceTemplate.Api   # Development: migrates on startup, Scalar at /scalar
```

## Tests

```bash
dotnet test        # unit + Testcontainers integration tests (Docker required)
```

## What is already wired in

| Concern | Where |
|---|---|
| Health: `/health/live` (process) and `/health/ready` (database) | `Infrastructure/HealthEndpoints.cs` |
| RFC 9457 ProblemDetails for every error, incl. 409 on write races | `Program.cs`, `Infrastructure/ConcurrencyConflictExceptionHandler.cs` |
| Versioned OpenAPI (`/openapi/v1.json`) + Scalar UI (`/scalar`) | `Program.cs` |
| Serilog JSON logs, OpenTelemetry traces + metrics (OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set) | `Infrastructure/ObservabilityExtensions.cs` |
| PostgreSQL schema owned by a migrator role; API runs with DML-only role | `deploy/postgres/init/01-roles.sh`, `docker-compose.yml` |
| Architecture decisions | `docs/adr/` |

The `Orders` feature is a sample. Replace it with your own domain, keep the plumbing.

## License

MIT
