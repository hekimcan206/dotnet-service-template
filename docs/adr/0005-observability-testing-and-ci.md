# 0005 — Observability, test stack and CI/supply-chain tooling

- Status: accepted
- Date: 2026-11-06

## Context

Every service copied from this template should, from day one, emit logs and telemetry that a
production team can use. It should be tested against the real database engine, and it should have
CI that catches regressions, vulnerable code and stale dependencies without much maintenance.

## Options considered

**Logging and telemetry**

1. `Microsoft.Extensions.Logging` JSON console with OpenTelemetry logs. One fewer dependency, but
   enrichment and per-namespace level configuration are weaker.
2. **Serilog (`AddSerilog` through DI) writing compact JSON to stdout, plus the OpenTelemetry SDK for
   traces and metrics.** Serilog 4 writes `@tr` and `@sp` (trace and span id) on every event, so logs
   and traces correlate without extra code.
3. An OpenTelemetry Collector sidecar in compose as a required component. It adds a moving part even
   for people who only want to call the API.

**Tests**

1. EF Core InMemory or SQLite for "integration" tests. Fast, but they do not test PostgreSQL behaviour
   (`xmin`, check constraints, roles, unique violations).
2. **Testcontainers (`postgres:17-alpine`) with WebApplicationFactory.** It uses the real engine and the
   real composition root, and costs about 7 s of container start-up per test run.
3. Assertion library: FluentAssertions 8 needs a commercial licence. **AwesomeAssertions** (an
   Apache-2.0 fork with the same API) and Shouldly are free. AwesomeAssertions was chosen.

**CI**

1. CodeQL default setup. No YAML, but no control over the build.
2. **CodeQL advanced setup with `build-mode: manual` for C#** (it observes the real `dotnet build`,
   which GitHub documents as the most precise mode) plus `actions` analysis of the workflows.
3. Releases and dependency updates: automated release and update tooling vs **manual updates and
   releases**. Tooling saves a few minutes per release, but it is one more moving part to configure
   and keep working for a template this size. Manual it is: the CHANGELOG is hand-written (Keep a
   Changelog), `<Version>` in `Directory.Build.props` is bumped by hand, and CI fails on
   known-vulnerable packages so that a needed update is not missed.

## Decision

- Serilog through DI, not a static bootstrap logger, so several `WebApplicationFactory` hosts can run
  in one process.
- OpenTelemetry covers ASP.NET Core, HttpClient, runtime, Npgsql (`ActivitySource`/`Meter` "Npgsql") and
  a business meter (`orders.created`, `orders.submitted`, `orders.submitted.value`). Health probes are
  filtered out of traces and logged at Verbose when they succeed.
- The OTLP exporter is enabled only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Compose has an opt-in
  `observability` profile that runs the Aspire dashboard as the OTLP backend.
- Tests use xunit.v3 on Microsoft.Testing.Platform (`global.json` → `"test": {"runner": ...}`;
  xunit.v3 4.x no longer supports the VSTest path on the .NET 10 SDK), FsCheck for property tests,
  and one assembly-scoped PostgreSQL container that is initialised with the same role script as compose.
- CI runs build (warnings are errors), tests with Cobertura coverage in the job summary, a container
  build with a non-root check, CodeQL (`security-and-quality`), and a vulnerable-package audit
  (`dotnet list package --vulnerable --include-transitive`).

## Consequences

- `dotnet test` needs Docker. Run unit tests alone with
  `dotnet test --project tests/ServiceTemplate.UnitTests`.
- The coverage numbers in the CI summary are measured on every run, and no badge claims a number the
  pipeline does not produce.
- Package, action and base-image updates are a maintainer task, and the audit step makes a known
  vulnerability fail the build instead of waiting for someone to notice.
