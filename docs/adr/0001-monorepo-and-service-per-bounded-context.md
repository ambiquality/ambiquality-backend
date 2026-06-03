# 1. Monorepo and service-per-bounded-context

- **Status:** Accepted
- **Date:** 2026-06
- **Context:** Ambiquality backend (bachelor thesis, VŠE Prague)

## Context

Ambiquality collects IEQ sensor measurements and republishes them as open data. The workload
has two sharply different shapes:

- a **write path** that must absorb bursty sensor traffic at ≥ 100 measurements/s and never
  acknowledge before a durable write, and
- a **read path** (the open-data API) that must serve ≥ 50 concurrent readers within
  p95 < 1 s / p99 < 3 s and stay ≥ 99 % available.

Around these sit authentication, a temporally-versioned catalog of buildings/rooms/sensors, and
a monthly archive exporter. Two structural questions had to be settled up front: **how many
deployable units** the system is split into, and **how the source is organised**.

## Decision

### A single repository of independently-deployable services

All services live in one Git repository and one solution (`ambiquality-backend.slnx`), but each
is its own deployable process with its own container image: `Auth.Api`, `Evidence.Api`,
`Ingestion.Api`, `Ingestion.Worker`, `Public.Api`, `Export.Worker`, plus the shared `Core`
library.

Rationale:

- **Atomic cross-service change.** A change to a shared contract (e.g. the
  `Core/Messaging/MeasurementMessage` queue contract, shared by `Ingestion.Api` and
  `Ingestion.Worker`) lands in one commit across producer and consumer, with no version-skew
  window.
- **`Core` as a project reference, not a package.** Sharing the measurement model, `IeqDbContext`
  and the queue contract via a plain project reference avoids standing up NuGet feeds and
  versioning a library that only this system consumes.
- **One clone for examiners.** A thesis must be reproducible from a single checkout; one repo +
  `./dev.sh up` brings up the whole topology.

Trade-off accepted: a change to `Core` rebuilds every dependent, and CI builds the whole solution
rather than only what changed. At this size that cost is negligible and is outweighed by the
atomicity benefit.

### Service-per-bounded-context, not a modular monolith

Each bounded context is a separate process behind a Caddy ingress, with Redis as the durable
ingestion queue and per-service migration containers — rather than a single ASP.NET process with
internal modules.

This is a **defended** choice, not a default. A modular monolith would be simpler to operate, and
for a project of this scale would be a legitimate option. It was rejected because the separation
*is* the thesis contribution:

- **Read/write scaling separation.** The write-heavy ingestion path and the read-heavy open-data
  API are independent processes, so each can be scaled and tuned without affecting the other —
  the central justification for the architecture. In a monolith they would share a process and
  contend for the same resources.
- **The durable queue path is first-class.** `Ingestion.Api` validates and enqueues to a Redis
  stream (returning **202**, or **503** if the enqueue fails) and never writes the database;
  `Ingestion.Worker` drains the stream and bulk-inserts. Making these separate deployables makes
  the accept-vs-persist boundary explicit and lets the write path be materialised by a worker
  that can crash and recover (`XAUTOCLAIM`) without affecting acceptance.
- **Least-privilege data access.** Three databases in one Postgres instance, each service
  connecting as its own role (`auth_api`, `evidence_api`, `ingestion_api` rw, `public_api` ro,
  `export_worker` ro), enforce isolation a single-process app would have to self-impose. There
  are **no cross-database foreign keys**: identity travels in the JWT `sub` claim and a
  measurement's `sensor_id` references the catalog logically.

## Consequences

- **Positive:** independent scaling and deployment of the read/write paths; an explicit, durable
  queue boundary; least-privilege per-service DB roles; an architecture that demonstrably
  exercises the thesis's non-functional goals.
- **Negative / cost:** more moving parts (six services + Caddy + Redis + Postgres + migration
  containers) to orchestrate and reason about; cross-cutting concerns (auth, OpenAPI, problem
  details) are repeated per service; local development needs the full compose stack.
- **Mitigations:** the monorepo keeps shared concerns in `Core` and changes atomic; `./dev.sh`
  wraps the compose topology; each service ships its own README and the schemas are documented in
  [`docs/er/`](../er/README.md).

## Related

Higher-level enterprise/architecture models (ArchiMate) live in a **separate ArchiMate
repository** (see the root `TODO.md`). This ADR is the code-repo-local decision record; the
ArchiMate models are the home for the broader views and should be kept consistent with it.
