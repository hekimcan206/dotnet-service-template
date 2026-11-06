# 0004 — Domain errors as values, ProblemDetails on the wire, URL-segment versioning

- Status: accepted
- Date: 2026-11-06

## Context

Clients need error responses they can parse and branch on. The OpenAPI document should list every
response an endpoint can return, and the API needs a way to make breaking changes later.

## Options considered

**Errors**

1. **Throw domain exceptions and map them in middleware.** Little code at each call site, but control
   flow becomes invisible, and expected outcomes such as "order already cancelled" pay the cost of an
   exception.
2. **Return `Result`/`DomainError` from the domain and map them to `TypedResults.Problem` in the
   endpoint.** Expected failures are visible in method signatures, and exceptions are left for bugs and
   infrastructure faults.

**Versioning**

1. **`Asp.Versioning.Http`** (query, header or media-type versioning, sunset policies). Powerful, but
   its OpenAPI integration has historically lagged new ASP.NET releases, and most services never need
   more than one live major version.
2. **A URL segment (`/api/v1`), a route group per version, and one OpenAPI document per group name.**
   Built in, explicit in logs and dashboards, and easy to route at a gateway.

## Decision

- `DomainError(Code, Message, Kind)` maps to 400, 404 or 409. The response is an RFC 9457 problem with
  a stable `code` extension (`order.not_draft`, `order.line_price_invalid`, …) and a `traceId` for
  correlation.
- Request validation uses .NET 10's built-in `AddValidation()` with DataAnnotations on the request
  records, which gives field-level `errors` in a 400. The domain still enforces every rule itself.
  For example, `1.005` passes `[Range]`, but the domain rejects it because it would round silently.
- `UseExceptionHandler()` and `UseStatusCodePages()` make unhandled exceptions, 404 and 405 responses
  ProblemDetails too. `ConcurrencyConflictExceptionHandler` turns lost write races into 409.
- Versioning uses `MapGroup("/api/v1").WithGroupName("v1")` and `AddOpenApi("v1")`. A future v2 adds a
  group and a document. Scalar is served at `/scalar` when `OpenApi:Enabled` is true. That is on in
  Development and in compose, and off by default elsewhere.

## Consequences

- `Results<Ok<T>, ProblemHttpResult>` return types document responses automatically, and integration
  tests assert the `code`, not the message.
- Range limits are parsed with the invariant culture (`ParseLimitsInInvariantCulture`). Without it, a
  `tr-TR` test host rejected `"0.01"`. That bug was caught by the integration tests.
- Header- or media-type versioning would need `Asp.Versioning`. Record that in a new ADR if a consumer
  ever needs it.
