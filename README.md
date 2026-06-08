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
| [`Ambiquality.Evidence.Api`](src/Ambiquality.Evidence.Api/README.md) | **Built** | Building, room & sensor registration / lifecycle catalog | F05–F09 |
| [`Ambiquality.Ingestion.Api`](src/Ambiquality.Ingestion.Api/README.md) | **Built** | Validates a measurement and enqueues it (202); never writes the DB | F10 |
| [`Ambiquality.Ingestion.Worker`](src/Ambiquality.Ingestion.Worker/README.md) | **Built** | Drains the Redis stream and bulk-inserts measurements into the hypertable | F10 |
| [`Ambiquality.Public.Api`](src/Ambiquality.Public.Api/README.md) | **Built** | Read-only public/open-data API (JSON/JSON-LD/CSV), DCAT-AP 3.0, OpenAPI | F11–F17 |
| [`Ambiquality.Export.Worker`](src/Ambiquality.Export.Worker/README.md) | **Built** | Publishes monthly downloadable archives (CSV + JSON-LD) to object storage | F17 |
| [`Ambiquality.Core`](src/Ambiquality.Core/README.md) | **Built** | Shared library: `IeqDbContext`, measurement & range models, queue contract | — |

Each `src/*` project has a matching test project under `tests/`.

## Prerequisites

- [Podman](https://podman.io/) with the Docker Compose CLI plugin (`docker-compose`)
- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [`dotnet-ef`](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) (only for creating migrations)

```bash
dotnet tool install --global dotnet-ef
```

## Quick start

All services run via Podman Compose. Configuration and secrets come from a gitignored `.env`
at the repo root. `.env.example` is the source of truth for which variables are required.

```bash
# 1. Create your local .env from the template
cp .env.example .env

# 2. Edit .env and set a real JWT_SECRET (a 32+ character random string)
#    e.g. `openssl rand -hex 32`. Adjust other values only if you need to.

# 3. Start / stop (development profile includes Mailpit for catching emails)
./dev.sh up        # start all services (foreground)
./dev.sh down      # stop all services and remove volumes

./dev-build.sh     # rebuild container images, then start (use after code changes)
```

> `.env` is gitignored and never committed — keep real secrets there. `.env.example` lists every
> variable the stack expects (with safe dev defaults) and is the source of truth for required
> configuration.

> **First-init note:** the per-service database role passwords (`AUTH_API_DB_PASSWORD`,
> `EVIDENCE_API_DB_PASSWORD`, …) are applied to Postgres only on its **first** initialization,
> when `init-databases.sh` runs against an empty data volume. Changing them later in `.env` will
> not update the existing roles — you must reset the volume with `./dev.sh down` (which removes
> volumes) and start again for the new passwords to take effect.

## Topology

[Caddy](https://caddyserver.com/) is the public ingress; the API services are not published
directly except where noted. Routing is defined in `conf/Caddyfile`.

Caddy's `handle_path` strips the matched prefix, so each service sees paths without it
(e.g. `/public/v1/observations` reaches Public.Api as `/v1/observations`).

| Endpoint | URL | Notes |
|----------|-----|-------|
| Auth API | <http://localhost:8080/auth/> | Caddy `/auth/*` → `auth-api:6100` |
| Evidence API | <http://localhost:8080/evidence/> | Caddy `/evidence/*` → `evidence-api:6200` |
| Ingestion API | <http://localhost:8080/ingestion/> | Caddy `/ingestion/*` → `ingestion-api:6300` |
| Public API | <http://localhost:8080/public/> | Caddy `/public/*` → `public-api:6400` |
| Mailpit (email UI) | <http://localhost:8025> | Catches all outgoing emails (dev profile) |
| PostgreSQL + TimescaleDB | internal | Exposed on a random host port for debugging |
| Redis | internal | **Durable ingestion queue** — Redis Streams + consumer groups, AOF `appendfsync always` |

The Ingestion.Worker and Export.Worker are background services with no HTTP ingress.

## Architecture & conventions

See the architecture decision records — [`0001 monorepo and service-per-bounded-context`](docs/adr/0001-monorepo-and-service-per-bounded-context.md)
and [`0002 Czech OFN address model`](docs/adr/0002-ofn-czech-address-model.md) — and [`docs/er/`](docs/er/README.md)
for the entity-relationship diagrams of the three schemas.

- **Three databases, one Postgres instance** (see `init-databases.sql`):
  `auth` (owned by Auth.Api), `evidence` (owned by Evidence.Api), and `ieq` (the
  TimescaleDB `measurements` hypertable + `parameter_ranges`). Each service connects as its own
  least-privilege role: `auth_api`, `evidence_api`, `ingestion_api` (rw on `ieq`), and
  `public_api` (ro on `ieq` + `evidence`). User identity never crosses a DB boundary as a
  foreign key — it travels in the JWT `sub` claim; a measurement's `sensor_id` references the
  evidence catalog with no cross-database FK.
- **Ingestion is a queue + worker write path.** Ingestion.Api validates a measurement
  synchronously, stamps `received_at`, and appends it to a durable Redis stream, returning
  **202 Accepted** (or **503** if the enqueue fails) — it never touches the `measurements`
  table. Ingestion.Worker drains the stream's consumer group and bulk-inserts into the
  hypertable (idempotent on the measurement id). This decouples accept-from-sensor from
  persist-to-DB so write spikes don't couple to request throughput.
- **Minimal APIs + Domain-Driven layering.** Built services use the layering
  `Api → Application → Domain ← Infrastructure`, with `Domain` free of framework dependencies.
- **OpenAPI.** Each service uses .NET 10 `AddOpenApi` and serves an interactive
  [Scalar](https://github.com/scalar/scalar) reference at `/scalar/v1`.
- **Errors as RFC 9457 ProblemDetails** with stable `urn:ambiquality:*` type URIs.
- **Open-data conformance.** The catalog is structurally **DCAT-AP 3.0** and partially aligned
  with the Czech **DCAT-AP-CZ / OFN** profile; full conformance is structurally impossible
  because it requires an OVM (public-authority) publisher identity the author does not hold. See
  the [Public.Api README](src/Ambiquality.Public.Api/README.md#catalog-conformance-dcat-ap-30--dcat-ap-cz).
- **EF Core migrations** are code-first and applied automatically at startup by per-service
  `migrate` / `evidence-migrate` / `ingestion-migrate` containers. Do not scaffold from an existing database.

## Running tests

```bash
dotnet test                                    # whole solution
dotnet test tests/Ambiquality.Evidence.Api.Tests   # a single project
```

## Releasing

Container images are published to the [GitHub Container Registry](https://ghcr.io)
(`ghcr.io/ambiquality/*`). Releases use **unified versioning** — one semantic version
stamps every image at once.

To cut a release, push an annotated `vMAJOR.MINOR.PATCH` tag from `main`:

```bash
git tag -a v1.2.0 -m "Release 1.2.0"
git push origin v1.2.0
```

The [`Release images to GHCR`](.github/workflows/release.yml) workflow then builds and
pushes all nine images in parallel, each tagged `1.2.0`, `1.2`, and `latest`:

| Image | Role |
|-------|------|
| `auth-api` / `auth-migrate` | Auth.Api + its EF migration bundle |
| `evidence-api` / `evidence-migrate` | Evidence.Api + its EF migration bundle |
| `ingestion-api` / `ingestion-migrate` | Ingestion.Api + its EF migration bundle |
| `ingestion-worker` | Drains the Redis stream → measurements hypertable |
| `public-api` | Read-only open-data API |
| `export-worker` | Monthly downloadable archives |

Deploy a released version with the GHCR compose file (pulls images instead of building):

```bash
TAG=1.2.0 podman compose -f compose.ghcr.yml up -d
```

> **One-time setup:** GHCR packages are created **private**. To serve the open-data
> backend, make each of the nine packages **public** (GitHub → *Packages* → package →
> *Package settings* → *Change visibility*), or link them to this repository so its
> visibility applies. The workflow only needs the repo's built-in `GITHUB_TOKEN` — no
> extra secrets.

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md), [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md), and the
[`LICENSE`](LICENSE).
