# Ambiquality.Ingestion.Api

The **write-only** service that receives observations from sensors and validates them at the
boundary. Implements thesis requirement **F10** (measurement validation on ingestion), specified
by use case **UC10**.

## Responsibility

Accept a **batch of observations** from one sensor, validate every reading against the sensor
catalog and configured ranges, and durably enqueue the batch before acknowledging — once
materialized the measurements are immutable (soft-invalidation only). A sensor that measures
several quantities reports them together; it lists only the parameters it actually measures and is
never forced to fill in the rest. The batch is **all-or-nothing**: one bad reading rejects the whole
request and nothing is enqueued. Built to the thesis non-functional constraints:

- **Durability before acknowledgement** — the batch is durably enqueued (Redis stream, AOF
  `appendfsync always`) before any 2xx; no ack-before-write. A multi-reading batch is appended inside
  one `MULTI`/`EXEC` transaction so it lands atomically.
- **Throughput** — designed to sustain ≥ 100 measurements/second (cheap SHA-256 key check, one
  catalog read per batch, an atomic append). Batching several quantities per request raises the
  effective rate further.
- **Immutability** — measurements are never updated or deleted; invalidation is a soft flag.

## Endpoint

| Method | Path | Auth | Success |
|--------|------|------|---------|
| `POST` | `/measurements` | `X-Sensor-Key` header | `202 Accepted` |

Request body:

```json
{
  "sensorId": "<guid>",
  "readings": [
    { "parameterCode": "co2", "value": 812, "unit": "ppm" },
    { "parameterCode": "temperature", "value": 21.5, "unit": "°C" },
    { "parameterCode": "humidity", "value": 45, "unit": "%" }
  ]
}
```

The sensor's secret key travels in the **`X-Sensor-Key`** header, never the body. The
observation time is **not** taken from the request: the sensor's clock is untrusted, so the
API stamps both `observedAt` and the acceptance `receivedAt` from the server clock at
acceptance — a single read shared by every reading in the batch, so they are all equal. Any
`observedAt` sent in the body is ignored. On success the response carries the shared
`receivedAt` and, for each reading, the measurement id it was assigned:

```json
{
  "receivedAt": "2026-05-27T10:00:00Z",
  "measurements": [
    { "id": "<guid>", "parameterCode": "co2" },
    { "id": "<guid>", "parameterCode": "temperature" },
    { "id": "<guid>", "parameterCode": "humidity" }
  ]
}
```

`202 Accepted`, not `201`: the batch is durably enqueued but not yet materialized into the
hypertable — Ingestion.Worker performs the write asynchronously.

## Validation pipeline (UC10)

The sensor is authenticated once for the batch, then every reading is validated; the **first**
failure short-circuits with a Problem Details response and nothing is enqueued:

| Step | Rule | Rejection | Status |
|------|------|-----------|--------|
| 0 | The batch carries at least one reading | empty batch | `422` |
| 1 | Sensor exists **and** `SHA-256(X-Sensor-Key)` matches `evidence.sensors.api_key_hash` | unknown sensor / bad key | `401` |
| 2 | Sensor's current status is `active` | not active | `403` |
| 3 | Each parameter appears at most once in the batch | duplicate parameter | `422` |
| 4 | Sensor declares each reading's `parameterCode` (open row) | parameter not declared | `422` |
| 5 | Each reading's `unit` matches the parameter's canonical unit (`ieq.parameter_ranges.unit`) | unit mismatch | `422` |
| 6 | Each value lies within `ieq.parameter_ranges` for its parameter | value out of range | `422` |
| 7 | Atomically enqueue the batch, then ack | — | `202` |

Every problem carries a stable `urn:ambiquality:ingestion:<reason>` `type`.

Unit matching (UC10 step 3's unit half) compares the reading's declared `unit` against the
parameter's **canonical unit** in `ieq.parameter_ranges` — each supported quantity has exactly
one unit in the platform vocabulary (mirroring `QudtVocabulary`), so declaring the parameter in
Evidence fixes the unit a sensor must report in. Comparison trims whitespace and folds the Greek
mu into the micro sign; case stays significant. The accepted measurement is stored with the
canonical unit string.

## Data access

- **Measurements & ranges** — read-write on the `ieq` schema via [`Core`](../Ambiquality.Core/README.md)'s
  `IeqDbContext`. This service owns the ieq migrations; the `ingestion-migrate` container applies them
  (it also converts `measurements` into a TimescaleDB hypertable and seeds `parameter_ranges`).
- **Sensor catalog** — read-only on the `evidence` schema (`ingestion_api` role) via a direct,
  schema-qualified SQL read (`Infrastructure/Catalog/SensorCatalog.cs`), not HTTP — chosen to keep
  the hot path cheap. There is no cross-database foreign key; the sensor `Id` is the only link.

## Running

```bash
dotnet run --project src/Ambiquality.Ingestion.Api      # needs ieq + evidence databases
```

In the container stack it sits behind Caddy at `/ingestion/*` → `ingestion-api:6300`. See the
[root README](../../README.md) for the overall project map.
