# Security policy

## Supported versions

Only the latest release of this template receives fixes. Services generated from it are owned by
their own repositories and must apply updates themselves. CI fails when a known-vulnerable package is
restored, so a new advisory shows up on the next build.

## Reporting a vulnerability

Please **do not open a public issue**. Use GitHub's private vulnerability reporting instead:
**Security → Report a vulnerability** on this repository. You can expect an acknowledgement within
5 working days. If the report is confirmed, a fix or mitigation plan follows within 30 days.

Please include the affected version or commit, the steps to reproduce, and the impact you observed.

## Security-relevant defaults in this template

| Area | Default |
|---|---|
| Container | Chiseled Ubuntu runtime image: no shell or package manager; runs as non-root UID 1654 |
| Database | API connects as `service_app` (DML only). Schema changes need `service_migrator`, used only by the one-shot `--migrate-only` process |
| Secrets | None in the repository. Compose defaults are for local development only and can be overridden through `.env` (see `.env.example`) |
| Errors | ProblemDetails responses never include exception details. Health responses omit failure descriptions |
| SQL | EF Core parameterises all queries. No string-built SQL |
| Supply chain | Centrally pinned package versions (`Directory.Packages.props`), updated by hand; CI fails on known-vulnerable direct or transitive packages (`dotnet list package --vulnerable --include-transitive`); CodeQL `security-and-quality` on every push and weekly. Private repositories without GitHub Code Security keep the SARIF as a workflow artifact; public repositories upload it to code scanning |
| API docs | OpenAPI/Scalar is off unless `OpenApi:Enabled=true` |

Not included, and to be added per service: authentication/authorisation, rate limiting, CORS policy
and TLS termination (normally at the ingress).
