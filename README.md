# ambiquality-backend

Backend services for **Ambiquality** — an open-source platform for collecting, storing, and
sharing Indoor Environment Quality (IEQ) measurements (CO₂, temperature, humidity, particulate
matter, VOCs, acoustics, light) from IoT sensors and publishing them as open data. Built as a
bachelor thesis at VŠE Prague (author: Vilém Charwot, submission May 2026).

## Projects

The solution (`ambiquality-backend.slnx`) is split into independently deployable services so
the write-heavy and read-heavy paths can scale separately. Each project has its own README.

| Project | Status | Responsibility | Thesis FRs |
|---------|--------|----------------|-----------|
| [`Ambiquality.Auth.Api`](src/Ambiquality.Auth.Api/README.md) | **Built** | Authentication & account management | F01–F04 |
| [`Ambiquality.Evidence.Api`](src/Ambiquality.Evidence.Api/README.md) | **Built** | Building & room registration / lifecycle catalog | F05–F09 |
| [`Ambiquality.Ingestion.Api`](src/Ambiquality.Ingestion.Api/README.md) | Skeleton | Write-only measurement ingestion & validation | F10 |
| [`Ambiquality.Public.Api`](src/Ambiquality.Public.Api/README.md) | Skeleton | Read-only public/open-data API, DCAT-AP-CZ, CSV export | F11–F17 |
| [`Ambiquality.Core`](src/Ambiquality.Core/README.md) | Empty | Planned shared library (`IeqDbContext`, measurement models) | — |

Each `src/*` project has a matching test project under `tests/`.

## Prerequisites

- [Podman](https://podman.io/) with the Docker Compose CLI plugin (`docker-compose`)
- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [`dotnet-ef`](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) (only for creating migrations)

```bash
dotnet tool install --global dotnet-ef
```

## Quick start

All services run via Podman Compose. Secrets come from a gitignored `.env` at the repo root.

```bash
# 1. Create .env
cat > .env <<'EOF'
JWT_SECRET=your-secret-key-at-least-32-characters-long
EOF

# 2. Start / stop (development profile includes Mailpit for catching emails)
./dev.sh up        # start all services (foreground)
./dev.sh down      # stop all services and remove volumes

./dev-build.sh     # rebuild container images, then start (use after code changes)
```

## Topology

[Caddy](https://caddyserver.com/) is the public ingress; the API services are not published
directly except where noted. Routing is defined in `conf/Caddyfile`.

| Endpoint | URL | Notes |
|----------|-----|-------|
| Auth API | <http://localhost:8080/> | Caddy default upstream → `auth-api:6100` |
| Evidence API | <http://localhost:8080/evidence/> | Caddy `/evidence/*` → `evidence-api:6200` |
| Evidence API (direct) | <http://localhost:6200/> | Published for convenience/dev |
| Mailpit (email UI) | <http://localhost:8025> | Catches all outgoing emails (dev profile) |
| PostgreSQL + TimescaleDB | internal | Exposed on a random host port for debugging |
| Redis | internal | Cache layer |

## Architecture & conventions

- **Two databases, one Postgres instance** (see `init-databases.sql`):
  `auth` (owned by Auth.Api) and `evidence` (owned by Evidence.Api). Each service connects as
  its own least-privilege role (`auth_api`, `evidence_api`). User identity never crosses a DB
  boundary as a foreign key — it travels in the JWT `sub` claim.
- **Minimal APIs + Domain-Driven layering.** Built services use the layering
  `Api → Application → Domain ← Infrastructure`, with `Domain` free of framework dependencies.
- **OpenAPI.** Each service uses .NET 10 `AddOpenApi` and serves an interactive
  [Scalar](https://github.com/scalar/scalar) reference at `/scalar/v1`.
- **Errors as RFC 9457 ProblemDetails** with stable `urn:ambiquality:*` type URIs.
- **EF Core migrations** are code-first and applied automatically at startup by a per-service
  `migrate` / `evidence-migrate` container. Do not scaffold from an existing database.

## Running tests

```bash
dotnet test                                    # whole solution
dotnet test tests/Ambiquality.Evidence.Api.Tests   # a single project
```

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md), [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md), and the
[`LICENSE`](LICENSE).
