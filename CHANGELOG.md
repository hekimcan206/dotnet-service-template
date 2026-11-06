# Changelog

All notable changes to this project are documented here. The file is maintained by hand, following
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.0] - 2026-11-06

### Added

- .NET 10 minimal API sample service: an order aggregate with invariants (draft → submitted → cancelled,
  SKU merge at a single price, two-decimal prices, line and quantity limits)
- PostgreSQL through EF Core 10 / Npgsql: a dedicated `service` schema, check constraints, and `xmin`
  optimistic concurrency, with write races returned as `409 Conflict`
- Least-privilege database roles: a one-shot `--migrate-only` process uses the schema owner, and the
  API runs with a DML-only role
- Health endpoints: `/health/live` (process only) and `/health/ready` (database check), with JSON bodies
- RFC 9457 ProblemDetails for all errors, with stable `code` and `traceId` extensions; .NET 10 built-in
  request validation
- Versioned OpenAPI document (`/openapi/v1.json`) and Scalar API reference (`/scalar`)
- Serilog compact JSON logs; OpenTelemetry traces and metrics (ASP.NET Core, HttpClient, runtime,
  Npgsql, business meter) with an opt-in OTLP exporter
- Multi-stage Dockerfile on `aspnet:10.0-noble-chiseled`, running as non-root
- `docker compose up` stack (postgres → migrator → api), plus an optional Aspire dashboard profile
- `dotnet new hekim-service` template with renaming, configurable ports and a fresh README/CHANGELOG
- Tests: xunit.v3 on Microsoft.Testing.Platform, AwesomeAssertions, FsCheck property tests, and
  Testcontainers PostgreSQL integration tests through WebApplicationFactory
- GitHub workflows: CI (build, test, coverage summary, container build), template instantiation check,
  CodeQL

[Unreleased]: https://github.com/hekimcan206/dotnet-service-template/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/hekimcan206/dotnet-service-template/releases/tag/v1.0.0
