# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## About

Backend for **Ambiquality** — an IEQ (Indoor Environmental Quality) monitoring platform built as a bachelor thesis at VŠE Prague (author: Vilém Charwot, submission May 2026). The system collects sensor measurements of indoor environmental parameters (CO₂, temperature, humidity, particulate matter, VOCs, acoustics, light) and exposes them as open data.

## Solution Structure

```
ambiquality-backend.slnx        ← single solution, two solution folders
src/
  Ambiquality.Core/             ← [BUILT] shared library: EF Core IeqDbContext, Measurement + ParameterRange models, queue message contract (Messaging/)
  Ambiquality.Auth.Api/         ← [BUILT] authentication service (register, login, token management)
  Ambiquality.Evidence.Api/     ← [BUILT] building, room & sensor registration / lifecycle catalog (F05–F09)
  Ambiquality.Ingestion.Api/    ← [BUILT] validates sensor measurements & enqueues them to Redis (F10/UC10); does NOT write the DB
  Ambiquality.Ingestion.Worker/ ← [BUILT] background service: drains the Redis stream and bulk-writes measurements to the ieq hypertable
  Ambiquality.Public.Api/       ← [BUILT] read-only open-data API: observations (JSON/JSON-LD/CSV), evidence catalog, DCAT-AP 3.0, OpenAPI (F11–F17)
  Ambiquality.Export.Worker/    ← [BUILT] background service: publishes monthly downloadable archives (CSV + JSON-LD, zipped) to object storage; records them in ieq.measurement_exports for Public.Api's DCAT distributions (F17)
tests/
  Ambiquality.Core.Tests/
  Ambiquality.Auth.Api.Tests/
  Ambiquality.Evidence.Api.Tests/
  Ambiquality.Ingestion.Api.Tests/
  Ambiquality.Ingestion.Worker.Tests/
  Ambiquality.Public.Api.Tests/
  Ambiquality.Export.Worker.Tests/
```

**Auth.Api**, **Evidence.Api**, **Ingestion.Api**, **Ingestion.Worker**, **Public.Api** and
**Export.Worker** are implemented, and `Core` holds the shared measurement model, `IeqDbContext`
and the queue message contract. See each project's `README.md` for detail.
Per-project READMEs and the root `README.md` are the human-facing docs; this file is the agent guide.

**Ingestion is a queue + worker write path.** Ingestion.Api accepts a *batch* of readings from one
sensor (`{ sensorId, readings: [{ parameterCode, value, unit }, …] }` — a sensor reports only the
quantities it measures) and validates them synchronously (authenticate sensor + active once, then per
reading: declared, unit matches the parameter's canonical unit in `ieq.parameter_ranges`, value in
range; the batch is all-or-nothing — one bad reading rejects the whole
request). It stamps `received_at` at acceptance (one clock read shared by the batch), then atomically
appends the readings to a durable Redis stream (`MULTI`/`EXEC` for multi-reading batches) and returns
**202 Accepted** — it never touches the `measurements` table. Ingestion.Worker drains the stream's
consumer group in batches and bulk-inserts into the `ieq` hypertable. See *Architecture Decisions →
Ingestion queue + worker*.

## Tech Stack

- **.NET 10**, ASP.NET Core minimal APIs
- **PostgreSQL + TimescaleDB** — time-series measurements
- **Redis** — durable ingestion queue (Streams + consumer groups, AOF `appendfsync always`); also available as a cache layer
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
| `evidence` | `evidence` | `evidence_api` | Evidence.Api — buildings, rooms, sensors + their attribute history; read-only by `public_api` and `export_worker` (sensor placement → feature of interest) |
| `ieq` | `ieq` | `ingestion_api` (rw), `public_api` (ro), `export_worker` (ro + INSERT on `measurement_exports`) | Ingestion.Worker writes the `measurements` hypertable; Ingestion.Api reads `parameter_ranges` for validation; Export.Worker reads measurements and records exports |

- The Postgres image is `timescale/timescaledb` (TimescaleDB preloaded) and the `evidence`
  database has the `btree_gist` extension enabled for temporal exclusion constraints.
- **Sensors are the canonical device registry.** Evidence.Api owns sensor (device) identity;
  ingested measurements reference a sensor's `Id` (GUID). There is no separate `devices`
  table — the originally-planned `ieq.devices` is superseded by `evidence.sensors`.
- **The `ieq` database is built.** `measurements` is a TimescaleDB hypertable (partitioned on
  `received_at`, composite key `(id, received_at)`) and `parameter_ranges` seeds the permitted
  value ranges. Ingestion.Api owns its migrations (`ingestion_api`, rw); Public.Api reads it
  (`public_api`, ro — SELECT on both `ieq` and `evidence`). Measurements carry `sensor_id` referencing the evidence catalog
  with no cross-database FK; Ingestion.Api validates against the catalog via a read-only SQL
  connection to the evidence schema (the `ingestion_api` role has SELECT there).
- **The `measurements` hypertable is written only by Ingestion.Worker**, which bulk-inserts drained
  queue batches with `ON CONFLICT (id, received_at) DO NOTHING` — the queue is at-least-once, so the
  measurement id (generated by the API at enqueue) makes redelivery idempotent (exactly-once effect).
  `received_at` is stamped by the API and stored to microsecond precision (the `timestamptz` limit).

### EF Core ownership

- `AuthDbContext` lives in **Ambiquality.Auth.Api** and owns the `auth` database; migrations
  run at startup via the `migrate` container.
- `EvidenceDbContext` lives in **Ambiquality.Evidence.Api** and owns the `evidence` database;
  migrations run at startup via the `evidence-migrate` container.
- `IeqDbContext` lives in **Ambiquality.Core** and owns the `ieq` database; **Ingestion.Api**
  holds its migrations (`MigrationsAssembly`) and runs them via the `ingestion-migrate` container.
  Ingestion.Worker references `IeqDbContext` (for reads in tests) but writes via raw Npgsql bulk
  inserts; it does **not** own or run migrations. Public.Api references it read-only.

## Key Functional Requirements (from thesis)

| ID | Responsibility | Service |
|----|---------------|---------|
| F01–F04 | User registration, login, logout, credential change | Auth.Api ✅ |
| F05–F09 | Building, room & sensor registration and lifecycle | **Evidence.Api** ✅ |
| F10 | Measurement validation on ingestion | Ingestion.Api (validate + enqueue) + Ingestion.Worker (persist) ✅ |
| F11–F15 | Public read API, filtering, pagination, search, OpenAPI spec | Public.Api ✅ |
| F16 | DCAT-AP catalog metadata publication | Public.Api ✅ |
| F17 | Downloadable data archive (CSV + JSON-LD) | Export.Worker (produces archives) + Public.Api (lists them) ✅ |
| F18 | (Frontend) Interactive map — not in this repo |

Note: F05–F09 were originally scoped to Public.Api but were implemented in a dedicated
`Evidence.Api` service instead.

## Key Non-Functional Constraints

- **Availability**: Public API ≥ 99% uptime per calendar month
- **Durability**: No ack before a durable write. Reinterpreted for the queue path as *durably
  enqueued before 2xx*: the API returns 202 only after the measurement is committed to the Redis
  stream (AOF `appendfsync always` = fsync per XADD); the stream is the write-ahead log and the
  worker's hypertable insert is its materialization. If the enqueue fails the API returns **503**
  and acks nothing.
- **Immutability**: Published measurements must never be silently modified or deleted; invalidation via explicit flag only
- **Performance**: Read API p95 < 1 s, p99 < 3 s for pages ≤ 100 records; ingestion ≥ 100 measurements/s sustained
- **Concurrency**: Read API must handle ≥ 50 concurrent requests within latency bounds

## Running Services

```bash
dotnet run --project src/Ambiquality.Auth.Api
dotnet run --project src/Ambiquality.Evidence.Api
dotnet run --project src/Ambiquality.Ingestion.Api      # validate + enqueue; needs evidence db + Redis
dotnet run --project src/Ambiquality.Ingestion.Worker   # drain + persist; needs ieq db + Redis
dotnet run --project src/Ambiquality.Public.Api         # read-only open data; needs ieq + evidence dbs
```

Start the full stack (Postgres, Redis, Caddy, Mailpit, the APIs, the ingestion worker, migrations)
with the dev helper, which wraps `podman compose --profile development`:

```bash
./dev.sh up        # start (foreground)
./dev.sh down      # stop and remove volumes
./dev-build.sh     # rebuild images then start (after code changes)
```

Behind Caddy (`:8080`): `/evidence/*` → `evidence-api:6200`, `/ingestion/*` → `ingestion-api:6300`,
`/public/*` → `public-api:6400`, everything else → `auth-api:6100`. Caddy's `handle_path` strips the
prefix, so set `PublicApi__BaseIri` for correct public-api linked-data IRIs.

## Running Tests

```bash
dotnet test
dotnet test tests/Ambiquality.Ingestion.Api.Tests   # single project
```

## Architecture Decisions

- **Service-per-bounded-context**: Independently deployable services (Auth, Evidence, the
  Ingestion write path and the Public read API) so write-heavy and read-heavy paths scale separately. Built
  services use DDD layering `Api → Application → Domain ← Infrastructure`.
- **Ingestion queue + worker write path**: Ingestion.Api validates synchronously and enqueues to a
  Redis stream; Ingestion.Worker drains it and bulk-writes to TimescaleDB. This decouples
  accept-from-sensor from persist-to-DB so write spikes don't couple to request throughput
  (NFR ≥ 100 measurements/s).
  - **Queue tech = Redis Streams** (already in the stack): consumer groups + acks + replay, AOF
    `appendfsync always` for per-write durability. The shared wire contract is
    `Core/Messaging/MeasurementMessage` (+ `MeasurementMessageSerializer`, JSON, round-trippable so
    `received_at` survives the queue byte-for-byte); stream key / group / batch sizing live in
    `MeasurementQueueOptions`. Producer: `Ingestion.Api/Infrastructure/Queue/RedisMeasurementQueuePublisher`
    (XADD). Consumer: `Ingestion.Worker/MeasurementDrainService` (XREADGROUP → write → XACK,
    `XAUTOCLAIM` to recover a crashed consumer's pending entries) + `MeasurementBatchWriter`
    (idempotent bulk insert).
  - **`received_at` is stamped by the API at acceptance**, before enqueue — so however long the
    queue takes to drain, the recorded ingestion time never shifts. The hypertable partitions on it.
  - **Ack semantics**: API returns **202 Accepted** (durably enqueued, not yet materialized), or
    **503** if the enqueue fails. The worker acks a stream entry only after its row is committed, so
    a crash between write and ack merely redelivers an entry the idempotent writer skips.
  - StackExchange.Redis has no blocking `XREADGROUP`; the drain loop polls on
    `MeasurementQueueOptions.BlockMilliseconds` when the stream is idle and loops immediately while
    entries are flowing.
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
- **No cross-database foreign keys**: User identity is propagated via the JWT `sub` claim (GUID); there is no FK to `auth`. Evidence.Api validates Auth.Api's tokens (shared `Jwt` secret) and maps each `sub` to a local `evidence.user_projections` row (lazy upsert in `CurrentUserMiddleware`); ownership/audit columns store that projection id. Mutations require a bearer token; reads are anonymous and return precise building coordinates (open data — there is no anonymization). Building addresses follow the Czech OFN *Adresy* standard, anchored on the RÚIAN address-point code.
- **Code-first migrations**: Schema is designed conceptually first, implemented via EF Core migrations; do not use `dotnet ef dbcontext scaffold`
- **Measurement immutability**: Soft-invalidation only — add an `is_invalid` flag and `invalidated_reason`, never DELETE or UPDATE measurement values

## Git rules

- Before any fresh changes, check the branch you are on.  
- Never make changes on `main` branch - it's protected.
- Always pull the `origin/main` before creating branches from `main`
- Always create a new branch based on `main` for a brand new feature and name it properly.
- In presence of not commited changes, ask the user if you should commit the changes first. If yes, then you should wait for the user to merge them into `main` and he should instruct you to continue.
