# 0003 — Multi-stage Dockerfile on the chiseled, non-root ASP.NET image

- Status: accepted
- Date: 2026-11-06

## Context

The service ships as a container. The runtime image decides attack surface, CVE noise in scanners,
size and start-up behaviour. From .NET 10 on, Microsoft's images default to Ubuntu 24.04 ("noble"),
and every .NET 8+ image includes a non-root `app` user (UID 1654). ASP.NET Core listens on 8080 by
default because a non-root user cannot bind ports below 1024.

## Options considered

1. **`aspnet:10.0` (full Ubuntu).** It has a shell and apt, which helps when debugging, but most of the
   packages it carries are never used by the app.
2. **`aspnet:10.0-alpine`.** Small, but musl-based. It still includes a shell and apk, and native
   dependencies sometimes behave differently on musl.
3. **`aspnet:10.0-noble-chiseled`.** Only the runtime's own dependencies: no shell, no package manager,
   non-root by default. `-extra` variants add ICU and tzdata when needed.
4. **`dotnet publish /t:PublishContainer` (SDK containers) with no Dockerfile.** Neat, but the build is
   less visible to reviewers, and multi-stage caching and labels are harder to customise.

## Decision

Option 3 with a two-stage Dockerfile:

- The SDK stage restores in its own layer (only `*.props`, `global.json` and the `.csproj` are copied
  first), using a BuildKit cache mount for NuGet.
- The runtime stage is `aspnet:10.0-noble-chiseled`, with `USER $APP_UID`, OCI labels and
  `EXPOSE 8080`.
- `InvariantGlobalization=true`, because the chiseled image has no ICU. It also keeps local and
  container behaviour the same.
- The compose file defines no container `HEALTHCHECK` (there is no shell or curl to run one).
  Orchestrators probe `/health/live` and `/health/ready` over HTTP.

## Consequences

- CI builds the image and fails if `Config.User` is empty or root.
- `docker exec -it … sh` does not work. Debug with `kubectl debug` or `docker debug`, or temporarily
  run the full image.
- A service that needs culture-aware formatting or time zones must switch to `noble-chiseled-extra`
  and remove `InvariantGlobalization`.
- Image tags are not pinned to digests, so a rebuild picks up the latest patched `10.0` base image.
  Pin digests when reproducible rebuilds matter more than picking up base-image patches on rebuild.
