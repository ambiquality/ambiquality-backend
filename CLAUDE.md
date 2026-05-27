# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## About

Backend for **Ambiquality** — an IEQ (Indoor Environmental Quality) monitoring platform built as a bachelor thesis at VŠE Prague (author: Vilém Charwot, submission May 2026). The system collects sensor measurements of indoor environmental parameters (CO₂, temperature, humidity, particulate matter, VOCs, acoustics, light) and exposes them as open data.

## Solution Structure

```
ambiquality-backend.slnx        ← single solution, two solution folders
src/
  Ambiquality.Core/             ← [BUILT] shared library: EF Core IeqDbContext, Measurement + ParameterRange models
  Ambiquality.Auth.Api/         ← [BUILT] authentication service (register, login, token management)
  Ambiquality.Evidence.Api/     ← [BUILT] building, room & sensor registration / lifecycle catalog (F05–F09)
  Ambiquality.Ingestion.Api/    ← [BUILT] write-only: validates & stores sensor measurements (F10/UC10)
  Ambiquality.Public.Api/       ← [SKELETON] read-only: public API, filtering, pagination, open data
tests/
  Ambiquality.Core.Tests/
  Ambiquality.Auth.Api.Tests/
  Ambiquality.Evidence.Api.Tests/
  Ambiquality.Ingestion.Api.Tests/
  Ambiquality.Public.Api.Tests/
```

**Auth.Api**, **Evidence.Api** and **Ingestion.Api** are implemented, and `Core` holds the shared
measurement model + `IeqDbContext`. `Public.Api` is still a default minimal-API skeleton. See each
project's `README.md` for detail. Per-project READMEs and the root `README.md` are the human-facing
docs; this file is the agent guide.

## Tech Stack

- **.NET 10**, ASP.NET Core minimal APIs
- **PostgreSQL + TimescaleDB** — time-series measurements
- **Redis** — cache layer
- **Caddy** — reverse proxy / ingress
- **Podman** — container runtime (not Docker)
- **EF Core** — code-first migrations, Npgsql provider
- **xUnit** — test framework

## Database Architecture

### Databases, one Postgres instance

Provisioned by `init-databases.sql` on first container start. **Current reality:**

| Database | Schema | Owner role | Used by |
|----------|--------|-----------|---------|
| `auth` | `auth` | `auth_api` | Auth.Api — users, password hashes, tokens |
| `evidence` | `evidence` | `evidence_api` | Evidence.Api — buildings, rooms, sensors + their attribute history |
| `ieq` | `ieq` | `ingestion_api` (rw), `public_api` (ro) | Ingestion.Api — `measurements` hypertable + `parameter_ranges` |

- The Postgres image is `timescale/timescaledb` (TimescaleDB preloaded) and the `evidence`
  database has the `btree_gist` extension enabled for temporal exclusion constraints.
- **Sensors are the canonical device registry.** Evidence.Api owns sensor (device) identity;
  ingested measurements reference a sensor's `Id` (GUID). There is no separate `devices`
  table — the originally-planned `ieq.devices` is superseded by `evidence.sensors`.
- **The `ieq` database is built.** `measurements` is a TimescaleDB hypertable (partitioned on
  `received_at`) and `parameter_ranges` seeds the permitted value ranges. Ingestion.Api owns its
  migrations (`ingestion_api`, rw); the planned Public.Api will read it (`public_api`, ro).
  Measurements carry `sensor_id` referencing the evidence catalog with no cross-database FK;
  Ingestion.Api validates against the catalog via a read-only SQL connection to the evidence
  schema (the `ingestion_api` role has SELECT there).

### EF Core ownership

- `AuthDbContext` lives in **Ambiquality.Auth.Api** and owns the `auth` database; migrations
  run at startup via the `migrate` container.
- `EvidenceDbContext` lives in **Ambiquality.Evidence.Api** and owns the `evidence` database;
  migrations run at startup via the `evidence-migrate` container.
- `IeqDbContext` lives in **Ambiquality.Core** and owns the `ieq` database; **Ingestion.Api**
  holds its migrations (`MigrationsAssembly`) and runs them via the `ingestion-migrate` container.
  The planned Public.Api will reference it read-only.

## Key Functional Requirements (from thesis)

| ID | Responsibility | Service |
|----|---------------|---------|
| F01–F04 | User registration, login, logout, credential change | Auth.Api ✅ |
| F05–F09 | Building, room & sensor registration and lifecycle | **Evidence.Api** ✅ |
| F10 | Measurement validation on ingestion | Ingestion.Api ✅ |
| F11–F15 | Public read API, filtering, pagination, search, OpenAPI spec | Public.Api (planned) |
| F16 | DCAT-AP-CZ catalog metadata publication | Public.Api (planned) |
| F17 | Downloadable data archive (CSV) | Public.Api (planned) |
| F18 | (Frontend) Interactive map — not in this repo |

Note: F05–F09 were originally scoped to Public.Api but were implemented in a dedicated
`Evidence.Api` service instead.

## Key Non-Functional Constraints

- **Availability**: Public API ≥ 99% uptime per calendar month
- **Durability**: Measurements must be persisted before HTTP 2xx is returned (no ack before write)
- **Immutability**: Published measurements must never be silently modified or deleted; invalidation via explicit flag only
- **Performance**: Read API p95 < 1 s, p99 < 3 s for pages ≤ 100 records; ingestion ≥ 100 measurements/s sustained
- **Concurrency**: Read API must handle ≥ 50 concurrent requests within latency bounds

## Running Services

```bash
dotnet run --project src/Ambiquality.Auth.Api
dotnet run --project src/Ambiquality.Evidence.Api
dotnet run --project src/Ambiquality.Ingestion.Api   # needs ieq + evidence databases
# Public.Api is still a skeleton (returns "Hello World!")
```

Start the full stack (Postgres, Redis, Caddy, Mailpit, the APIs, migrations) with the dev
helper, which wraps `podman compose --profile development`:

```bash
./dev.sh up        # start (foreground)
./dev.sh down      # stop and remove volumes
./dev-build.sh     # rebuild images then start (after code changes)
```

Behind Caddy (`:8080`): `/evidence/*` → `evidence-api:6200`, `/ingestion/*` → `ingestion-api:6300`,
everything else → `auth-api:6100`.

## Running Tests

```bash
dotnet test
dotnet test tests/Ambiquality.Ingestion.Api.Tests   # single project
```

## Architecture Decisions

- **Service-per-bounded-context**: Independently deployable services (Auth, Evidence, and the
  planned Ingestion/Public split) so write-heavy and read-heavy paths scale separately. Built
  services use DDD layering `Api → Application → Domain ← Infrastructure`.
- **Attribute-level temporal versioning (Evidence.Api)**: Building, room and sensor attributes
  are not mutable columns — each is a stream of history rows carrying a half-open UTC `tstzrange`
  validity period (`Domain/Common/Validity.cs`). Changes close the open row and open a new one;
  nothing is overwritten. `btree_gist` exclusion constraints forbid overlapping periods. Reads
  accept an `asOf` query param to project past state. Rationale: the open-data catalog needs an
  immutable, auditable record of how each object changed over time.
  - **Closing a row uses an exclusive upper bound** (`[lower, validFrom)`); the raw 2-arg
    `NpgsqlRange` ctor makes it *inclusive*, which overlaps the next row's lower bound. All three
    aggregates (building, room, sensor) close correctly — buildings/rooms via the
    `Validity.Closed` factory, sensors via an explicit half-open ctor. See
    `Evidence.Api/README.md` → *Temporal integrity*.
  - **Exclusion constraints are `DEFERRABLE INITIALLY DEFERRED`** on every history table
    (building, room and sensor): a change emits an UPDATE (close) + INSERT (open) in one
    transaction and EF may order the INSERT first, so the no-overlap check must run at COMMIT.
    Single-value attributes exclude on `(id, validity)`; collections add the item code (e.g.
    `(sensor_id, parameter_code, validity)`, `(room_id, source_code, validity)`).
- **Sensors = canonical device registry**: Evidence owns sensor/device identity; planned
  ingestion measurements reference `sensor_id` (GUID), no separate devices table.
- **TimescaleDB**: Chosen for time-series performance on `measurements` hypertable; use TimescaleDB-specific functions (time_bucket, continuous aggregates) where appropriate
- **No cross-database foreign keys**: User identity is propagated via the JWT `sub` claim (GUID); there is no FK to `auth`. Evidence.Api validates Auth.Api's tokens (shared `Jwt` secret) and maps each `sub` to a local `evidence.user_projections` row (lazy upsert in `CurrentUserMiddleware`); ownership/audit columns store that projection id. Mutations require a bearer token; reads are anonymous but mask non-owners' building coordinates per `anonymization_level`.
- **Code-first migrations**: Schema is designed conceptually first, implemented via EF Core migrations; do not use `dotnet ef dbcontext scaffold`
- **Measurement immutability**: Soft-invalidation only — add an `is_invalid` flag and `invalidated_reason`, never DELETE or UPDATE measurement values

## Git rules

- Before any fresh changes, check the branch you are on.  
- Never make changes on `main` branch - it's protected.
- Always pull the `origin/main` before creating branches from `main`
- Always create a new branch based on `main` for a brand new feature and name it properly.
- In presence of not commited changes, ask the user if you should commit the changes first. If yes, then you should wait for the user to merge them into `main` and he should instruct you to continue.
