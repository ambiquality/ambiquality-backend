# Ambiquality.Ingestion.Api

> **Status: skeleton.** Currently a default minimal-API template (`Program.cs` returns
> `"Hello World!"`). The functionality below is planned, not yet built.

## Intended responsibility

The **write-only** service that receives measurements from IoT sensors. Covers thesis
requirement **F10** (measurement validation on ingestion).

Planned characteristics, from the thesis non-functional constraints:

- **Validation on ingestion** — reject malformed or out-of-range measurements at the boundary.
- **Durability before acknowledgement** — a measurement must be persisted before any HTTP 2xx
  is returned; no ack-before-write.
- **Throughput** — sustain ≥ 100 measurements/second.
- **Immutability** — published measurements are never silently updated or deleted; invalidation
  is an explicit soft flag only.

It would write to the `measurements` hypertable (TimescaleDB) and own the EF migrations for the
measurement database (via [`Core`](../Ambiquality.Core/README.md)).

See the [root README](../../README.md) for the overall project map.
