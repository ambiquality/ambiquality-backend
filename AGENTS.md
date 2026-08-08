# AGENTS.md

Agent guide for this repository (read by opencode and Claude Code). The human-facing docs —
setup, scripts, env — live in [`README.md`](README.md); each `src/*` project has its own
`README.md`.

## About

Backend for **Ambiquality** — an IEQ (Indoor Environmental Quality) monitoring platform built
as a bachelor thesis at VŠE Prague (author: Vilém Charwot, submitted May 2026). The system
collects sensor measurements of indoor environmental parameters (CO₂, temperature, humidity,
particulate matter, VOCs, acoustics, light) and exposes them as open data.

## Solution structure

`ambiquality-backend.slnx` — single solution, two folders (`/src/`, `/tests/`):

```
src/
  Ambiquality.Core/             shared library: EF Core IeqDbContext, Measurement + ParameterRange
                                models, queue message contract (Messaging/)
  Ambiquality.Auth.Api/         authentication service (register, login, token management)
  Ambiquality.Evidence.Api/     building, room & sensor registration / lifecycle catalog (F05–F09)
  Ambiquality.Ingestion.Api/    validates sensor measurements & enqueues them to Redis (F10); does NOT write the DB
  Ambiquality.Ingestion.Worker/ background service: drains the Redis stream and bulk-writes measurements to the ieq hypertable
  Ambiquality.Public.Api/       read-only open-data API: observations (JSON/JSON-LD/CSV), evidence catalog, DCAT-AP 3.0, OpenAPI (F11–F17)
  Ambiquality.Export.Worker/    background service: publishes monthly downloadable archives (CSV + JSON-LD, zipped) to object storage;
                                records them in ieq.measurement_exports for Public.Api's DCAT distributions (F17)
  Ambiquality.Observability/    shared OpenTelemetry metrics: ambiquality meter + instruments, OTel/Prometheus wiring (Golden Signals)
tests/
  Ambiquality.Core.Tests / Auth.Api.Tests / Evidence.Api.Tests / Ingestion.Api.Tests /
  Ingestion.Worker.Tests / Public.Api.Tests / Export.Worker.Tests / Observability.Tests
```

All services are implemented; `Core` holds the shared measurement model, `IeqDbContext` and the
queue message contract. Each project follows DDD layering `Api → Application → Domain ← Infrastructure`.

## Ingestion is a queue + worker write path

Ingestion.Api accepts a *batch* of readings from one sensor
(`{ sensorId, readings: [{ parameterCode, value, unit }, …] }` — a sensor reports only the
quantities it measures) and validates synchronously:

1. authenticate sensor + active once,
2. per-sensor publish rate-limit check,
3. per reading: declared, unit matches the parameter's canonical unit in `ieq.parameter_ranges`,
   value in range.

The batch is **all-or-nothing** — one bad reading rejects the whole request. The rate limit
(keyed by sensor id, Redis fixed-window) caps a sensor to `PermitsPerWindow` batches per its
declared reporting interval (`measurement_frequency_seconds` on the open
`evidence.sensor_installation_history` row, clamped to a 5-min floor, default 5 min); exceeding
it returns **429** + `Retry-After`.

It stamps `received_at` at acceptance (one clock read shared by the batch), then atomically
appends the readings to a durable Redis stream (`MULTI`/`EXEC` for multi-reading batches) and
returns **202 Accepted** — it never touches the `measurements` table. Ingestion.Worker drains
the stream's consumer group in batches and bulk-inserts into the `ieq` hypertable
(idempotent, `ON CONFLICT (id, received_at) DO NOTHING`). See *Architecture decisions → Ingestion
queue + worker*.

## Tech stack

- **.NET 10**, ASP.NET Core minimal APIs
- **PostgreSQL + TimescaleDB** — time-series measurements
- **Redis** — durable ingestion queue (Streams + consumer groups, AOF `appendfsync always`); also available as a cache layer
- **Caddy** — reverse proxy / ingress
- **Podman** — container runtime (not Docker)
- **EF Core** — code-first migrations, Npgsql provider
- **xUnit** — test framework

## Database architecture

### Databases, one Postgres instance

Provisioned by `init-databases.sql` on first container start:

| Database | Schema | Owner role | Used by |
|----------|--------|-----------|---------|
| `auth` | `auth` | `auth_api` | Auth.Api — users, password hashes, tokens |
| `evidence` | `evidence` | `evidence_api` | Evidence.Api — buildings, rooms, sensors + their attribute history; read-only by `public_api` and `export_worker` (sensor placement → feature of interest) |
| `ieq` | `ieq` | `ingestion_api` (rw), `public_api` (ro), `export_worker` (ro + INSERT on `measurement_exports`) | Ingestion.Worker writes the `measurements` hypertable; Ingestion.Api reads `parameter_ranges` for validation; Export.Worker reads measurements and records exports |

- The Postgres image is `timescale/timescaledb`; the `evidence` database has the `btree_gist`
  extension for temporal exclusion constraints.
- **Sensors are the canonical device registry.** Evidence.Api owns sensor (device) identity;
  ingested measurements reference a sensor's `Id` (GUID). There is no separate `devices`
  table — the originally-planned `ieq.devices` is superseded by `evidence.sensors`.
- The `measurements` hypertable is partitioned on `received_at` (composite key `(id, received_at)`)
  and written **only by Ingestion.Worker**. `received_at` is stamped by the API and stored to
  microsecond precision (the `timestamptz` limit).
- `parameter_ranges` seeds the permitted value ranges. Ingestion.Api owns its migrations
  (`ingestion_api`, rw); Public.Api reads it (`public_api`, ro — SELECT on both `ieq` and
  `evidence`). Measurements reference the evidence catalog with no cross-database FK;
  Ingestion.Api validates against the catalog via a read-only SQL connection to the evidence
  schema (the `ingestion_api` role has SELECT there).
- **Backups**: a `postgres-backup` sidecar (built from `backup/Dockerfile`) dumps all three
  databases + cluster globals on `BACKUP_INTERVAL_SECONDS` into the `backup-data` volume and
  optionally copies to S3 when `BACKUP_S3_*` is set (production must set this — SPO-04).

### EF Core ownership

- `AuthDbContext` lives in **Ambiquality.Auth.Api** and owns the `auth` database; migrations
  run at startup via the `migrate` container.
- `EvidenceDbContext` lives in **Ambiquality.Evidence.Api** and owns the `evidence` database;
  migrations run at startup via the `evidence-migrate` container.
- `IeqDbContext` lives in **Ambiquality.Core** and owns the `ieq` database; **Ingestion.Api**
  holds its migrations (`MigrationsAssembly`) and runs them via the `ingestion-migrate` container.
  Ingestion.Worker references `IeqDbContext` (for reads in tests) but writes via raw Npgsql bulk
  inserts; it does **not** own or run migrations. Public.Api references it read-only.

## Functional requirements (from thesis)

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

## Non-functional constraints

- **Availability**: Public API ≥ 99% uptime per calendar month
- **Durability**: No ack before a durable write. For the queue path: the API returns 202 only
  after the measurement is committed to the Redis stream (AOF `appendfsync always` = fsync per
  XADD); the stream is the write-ahead log, the worker's hypertable insert its materialization.
  If the enqueue fails the API returns **503** and acks nothing.
- **Immutability**: Published measurements must never be silently modified or deleted; invalidation via explicit flag only
- **Performance**: Read API p95 < 1 s, p99 < 3 s for pages ≤ 100 records; ingestion ≥ 100 measurements/s sustained
- **Concurrency**: Read API must handle ≥ 50 concurrent requests within latency bounds

## Observability (Four Golden Signals)

Services export OpenTelemetry metrics (`Ambiquality.Observability`) on dedicated internal
`/metrics` ports (auth 9464, evidence 9465, ingestion 9466, public 9467, ingestion-worker 9468,
export-worker 9469) via an `HttpListener` — never the app port, so `/metrics` can't leak through
Caddy. `compose.monitoring.yml` + `conf/` add Prometheus + Grafana (4 dashboards: Overview,
Service RED, Infrastructure USE, Ingestion Pipeline) accessed **only via SSH port forwarding**
(`ssh -L 3000:… -L 9090:…`). See `docs/monitoring.md`.

- **Metrics add sites**: instruments live in `Ambiquality.Observability/AmbiqualityMetrics.cs`;
  recorded e.g. at `MeasurementEndpoints.RecordResult` (ingestion outcomes),
  `CurrentUserMiddleware` + `ActiveUsersTracker` (operator activity), `QueueMetricsService`
  + `DrainStatus` (Ingestion.Worker), `RumVitalsEndpoint` (Public.Api web vitals) and
  `MonthlyExportService` (exports). Prometheus labels every target with a static `service`
  label in `conf/prometheus/prometheus.yml` — keep new targets and dashboards' `{__name__=…}`
  matchers in sync.
- **Public.Api RUM endpoint**: `POST /telemetry/vitals` (CORS-open, `ExcludeFromDescription`
  so it stays out of the published OpenAPI) feeds `ambiquality.web_vitals.*`. Keep
  `VITE_RUM_ENDPOINT` in the frontend release pipeline aligned with its routing
  (`…/public/telemetry/vitals`).
- **Tests**: `Ambiquality.Observability.Tests` (`RollingActivityGauge` window logic) and
  `Ambiquality.Public.Api.Tests/RumVitalsEndpointTests` (beacon validation + OpenAPI exclusion).
  Test factories set `Observability:Enabled=false` so no metrics listener binds a fixed port
  during tests.

## Running services

```bash
dotnet run --project src/Ambiquality.Auth.Api
dotnet run --project src/Ambiquality.Evidence.Api
dotnet run --project src/Ambiquality.Ingestion.Api      # validate + enqueue; needs evidence db + Redis
dotnet run --project src/Ambiquality.Ingestion.Worker   # drain + persist; needs ieq db + Redis
dotnet run --project src/Ambiquality.Public.Api         # read-only open data; needs ieq + evidence dbs
dotnet run --project src/Ambiquality.Export.Worker      # monthly archives; needs ieq + evidence dbs + storage
```

Start the full stack (Postgres, Redis, Caddy, Mailpit, the APIs, the workers, migrations) with
the dev helper, which wraps `podman compose --profile development`:

```bash
./dev.sh up        # start (foreground)
./dev.sh down      # stop and remove volumes
./dev-build.sh     # rebuild images then start (after code changes)
```

Behind Caddy (`:8080`): `/evidence/*` → `evidence-api:6200`, `/ingestion/*` → `ingestion-api:6300`,
`/public/*` → `public-api:6400`, everything else → `auth-api:6100`. Caddy's `handle_path` strips the
prefix, so set `PublicApi__BaseIri` for correct public-api linked-data IRIs.

## Verification gates

```bash
dotnet build                     # whole solution
dotnet test                      # whole test suite
dotnet test tests/Ambiquality.Evidence.Api.Tests   # a single project
```

`dotnet test` must pass before considering work done. Use Podman (not Docker) for anything
container-related.

## Architecture decisions

- **Service-per-bounded-context**: Independently deployable services (Auth, Evidence, the
  Ingestion write path and the Public read API) so write-heavy and read-heavy paths scale
  separately. Built services use DDD layering `Api → Application → Domain ← Infrastructure`.
- **Ingestion queue + worker write path**: Ingestion.Api validates synchronously and enqueues to a
  Redis stream; Ingestion.Worker drains it and bulk-writes to TimescaleDB. This decouples
  accept-from-sensor from persist-to-DB so write spikes don't couple to request throughput
  (NFR ≥ 100 measurements/s).
  - **Queue tech = Redis Streams**: consumer groups + acks + replay, AOF `appendfsync always`
    for per-write durability. The shared wire contract is
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
- **TimescaleDB**: Chosen for time-series performance on `measurements` hypertable; use
  TimescaleDB-specific functions (time_bucket, continuous aggregates) where appropriate.
- **No cross-database foreign keys**: User identity is propagated via the JWT `sub` claim (GUID);
  there is no FK to `auth`. Evidence.Api validates Auth.Api's tokens (shared `Jwt` secret) and
  maps each `sub` to a local `evidence.user_projections` row (lazy upsert in
  `CurrentUserMiddleware`); ownership/audit columns store that projection id. Mutations require a
  bearer token; reads are anonymous and return precise building coordinates (open data — there is
  no anonymization). Building addresses follow the Czech OFN *Adresy* standard, anchored on the
  RÚIAN address-point code. Evidence.Api also exposes RÚIAN address-lookup endpoints
  (`/v1/address-lookup/suggest|resolve`) proxying ČÚZK's geocoder for the frontend autocomplete.
- **Code-first migrations**: Schema is designed conceptually first, implemented via EF Core
  migrations; do not use `dotnet ef dbcontext scaffold`.
- **Measurement immutability**: Soft-invalidation only — add an `is_invalid` flag and
  `invalidated_reason`, never DELETE or UPDATE measurement values.

## Git rules

- Before any fresh changes, check the branch you are on.
- Never make changes on `main` branch — it's protected.
- Always pull `origin/main` before creating branches from `main`.
- Always create a new branch based on `main` for a brand-new feature and name it properly.
- In the presence of uncommitted changes, ask the user whether to commit first. If yes, wait for
  the user to merge them into `main` and to instruct you to continue.

<!-- code-review-graph MCP tools -->
## MCP Tools: code-review-graph

**IMPORTANT: This project has a knowledge graph. ALWAYS use the
code-review-graph MCP tools BEFORE using Grep/Glob/Read to explore
the codebase.** The graph is faster, cheaper (fewer tokens), and gives
you structural context (callers, dependents, test coverage) that file
scanning cannot.

### When to use graph tools FIRST

- **Exploring code**: `semantic_search_nodes_tool` or `query_graph_tool` instead of Grep
- **Understanding impact**: `get_impact_radius_tool` instead of manually tracing imports
- **Code review**: `detect_changes_tool` + `get_review_context_tool` instead of reading entire files
- **Finding relationships**: `query_graph_tool` with callers_of/callees_of/imports_of/tests_for
- **Architecture questions**: `get_architecture_overview_tool` + `list_communities_tool`

Fall back to Grep/Glob/Read **only** when the graph doesn't cover what you need.

### Key Tools

| Tool | Use when |
| ------ | ---------- |
| `detect_changes_tool` | Reviewing code changes — gives risk-scored analysis |
| `get_review_context_tool` | Need source snippets for review — token-efficient |
| `get_impact_radius_tool` | Understanding blast radius of a change |
| `get_affected_flows_tool` | Finding which execution paths are impacted |
| `query_graph_tool` | Tracing callers, callees, imports, tests, dependencies |
| `semantic_search_nodes_tool` | Finding functions/classes by name or keyword |
| `get_architecture_overview_tool` | Understanding high-level codebase structure |
| `refactor_tool` | Planning renames, finding dead code |

### Semantic search: embeddings config (IMPORTANT)

The graph's vectors are computed with the **openai** provider using
`nomic-embed-text-v2-moe` (served via a local OpenAI-compatible endpoint). The MCP server
inherits the config from the shell env:

- `CRG_OPENAI_BASE_URL=http://10.0.0.1:11434/v1`
- `CRG_OPENAI_MODEL=nomic-embed-text-v2-moe`
- `CRG_OPENAI_API_KEY` (any non-empty value for the local endpoint)
- `CRG_OPENAI_BATCH_SIZE`

**Gotcha:** `semantic_search_nodes_tool` defaults to `provider="local"`
(all-MiniLM-L6-v2), which has **no** matching vectors in this graph, so it silently falls
back to keyword/FTS and returns 0 for fuzzy queries. To use the real vectors, **always pass
`provider="openai"`** — the model auto-falls back to `CRG_OPENAI_MODEL`, so the model
argument is not needed:

```
semantic_search_nodes_tool(query="...", provider="openai")
```

The provider is stored per-vector; a re-embed with a different provider/model/endpoint is
refused (migration), so keep `provider="openai"` and `CRG_OPENAI_MODEL` in sync with what
was used for `code-review-graph embed`.

### Workflow

1. The graph auto-updates on file changes (via hooks).
2. Use `detect_changes_tool` for code review.
3. Use `get_affected_flows_tool` to understand impact.
4. Use `query_graph_tool` pattern="tests_for" to check coverage.
