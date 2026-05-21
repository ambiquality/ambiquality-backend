# Ambiquality.Public.Api

> **Status: skeleton.** Currently a default minimal-API template (`Program.cs` returns
> `"Hello World!"`). The functionality below is planned, not yet built.

## Intended responsibility

The **read-only** public, open-data API. Covers thesis requirements **F11–F17**:

- public read API with filtering, pagination, search, and an OpenAPI spec (**F11–F15**),
- DCAT-AP-CZ catalog metadata publication (**F16**),
- downloadable data archive as CSV (**F17**).

Planned characteristics, from the thesis non-functional constraints:

- **Availability** ≥ 99% uptime per calendar month.
- **Performance** — read p95 < 1 s, p99 < 3 s for pages ≤ 100 records.
- **Concurrency** — ≥ 50 concurrent requests within latency bounds (Redis cache layer).

It would reference the measurement database read-only (via
[`Core`](../Ambiquality.Core/README.md)) and never run migrations.

> **Scope note.** Building and room registration (**F05–F09**) was originally planned to live
> here. It now lives in the dedicated
> [`Evidence.Api`](../Ambiquality.Evidence.Api/README.md).

See the [root README](../../README.md) for the overall project map.
