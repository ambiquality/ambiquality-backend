# Ambiquality.Ingestion.Api

The **write-only** service that receives observations from sensors and validates them at the
boundary. Implements thesis requirement **F10** (measurement validation on ingestion), specified
by use case **UC10**.

## Responsibility

Accept a single observation, validate it against the sensor catalog and configured ranges, and
persist it durably before acknowledging — then it is immutable (soft-invalidation only). Built to
the thesis non-functional constraints:

- **Durability before acknowledgement** — the measurement is written inside the request, before any
  2xx; no ack-before-write.
- **Throughput** — designed to sustain ≥ 100 measurements/second (cheap SHA-256 key check, a single
  catalog read, one insert).
- **Immutability** — measurements are never updated or deleted; invalidation is a soft flag.

## Endpoint

| Method | Path | Auth | Success |
|--------|------|------|---------|
| `POST` | `/measurements` | `X-Sensor-Key` header | `201 Created` |

Request body:

```json
{ "sensorId": "<guid>", "parameterCode": "co2", "value": 812 }
```

The sensor's secret key travels in the **`X-Sensor-Key`** header, never the body. The
observation time is **not** taken from the request: the sensor's clock is untrusted, so the
API stamps both `observedAt` and the acceptance `receivedAt` from the server clock at
acceptance (a single read, so they are equal). Any `observedAt` sent in the body is ignored.
On success the response carries the new measurement id and its server-side `receivedAt`.

## Validation pipeline (UC10)

In order, each failure short-circuits with a Problem Details response:

| Step | Rule | Rejection | Status |
|------|------|-----------|--------|
| 1 | Sensor exists **and** `SHA-256(X-Sensor-Key)` matches `evidence.sensors.api_key_hash` | unknown sensor / bad key | `401` |
| 2 | Sensor's current status is `active` | not active | `403` |
| 3 | Sensor declares the observation's `parameterCode` (open row) | parameter not declared | `422` |
| 4 | Value lies within `ieq.parameter_ranges` for that parameter | value out of range | `422` |
| 5 | Persist to the `ieq.measurements` hypertable, then ack | — | `201` |

Unit matching (UC10 step 3's unit half) is **deferred** until F08 (measured-parameter units) lands
in Evidence; only quantity declaration and value range are checked today.

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
