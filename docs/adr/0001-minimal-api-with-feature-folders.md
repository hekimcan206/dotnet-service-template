# 0001 — Minimal APIs in one project, organised by feature

- Status: accepted
- Date: 2026-11-06

## Context

This repository is the starting point for every later service. It has to be small enough to read in
one sitting, and it must still show where domain rules, persistence and HTTP concerns go, so that
copies do not grow into a tangle. Most services in the portfolio have one bounded context and a
handful of endpoints.

## Options considered

1. **Controllers + MVC filters.** Familiar and feature-complete, but it brings reflection-based
   discovery, more ceremony per endpoint and a second model (filters, model binding) on top of the
   routing that ASP.NET Core already has.
2. **Clean Architecture split into 4 projects (Domain / Application / Infrastructure / Api).** It
   enforces dependency direction at compile time. For a service with one aggregate it mostly adds
   project files, mapping layers and indirection. The boundaries it protects can be kept by
   convention plus review until a service actually needs them.
3. **Minimal APIs in one project with feature folders (`Domain/`, `Persistence/`, `Features/<Name>/`,
   `Infrastructure/`).** Endpoints are plain static methods returning `Results<...>` union types, so
   the OpenAPI document lists every status code without attributes. Domain types have no ASP.NET or
   EF dependencies apart from a private constructor that EF needs.

## Decision

Option 3. Business rules live in the aggregate (`Order`) and return `Result`/`DomainError` values.
Endpoint handlers load the aggregate, call one domain method, save, and map errors to ProblemDetails
(see [0004](0004-errors-and-api-versioning.md)).

## Consequences

- One project to build, publish and containerise. Endpoint handlers are `internal static`, so tests
  can call them directly when that is useful.
- Nothing stops `Domain/` from referencing `Persistence/`, so that rule relies on code review. Split the
  domain into its own project when a second consumer appears, such as a worker or a message handler.
  The folders already line up with that split.
- Template users delete `Features/Orders` and keep the plumbing.
