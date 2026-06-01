# Ambiquality.Public.Api

> **Status: built.** The read-only, fully-public open-data API. No authentication,
> no rate limiting (OFN open-data), CC BY 4.0 on every response.

Covers thesis requirements **F11–F17**.

## Responsibility

Serves IEQ measurements as SSN/SOSA observations and the evidence catalog
(buildings, rooms, sensors) over versioned REST endpoints with content negotiation
(plain JSON, JSON-LD, CSV), a DCAT-AP 3.0 catalog, and an OpenAPI spec + Scalar UI.

- Reads the `ieq` measurements hypertable read-only via `IeqDbContext`
  ([`Core`](../Ambiquality.Core/README.md)); never runs migrations.
- Reads the `evidence` catalog read-only via raw, schema-qualified Npgsql
  (`EvidenceCatalog`), the same pattern as `Ingestion.Api`'s `SensorCatalog`.
  The `public_api` role has SELECT on both schemas.

## Endpoints (all `GET` + `HEAD`, under `/v1`)

| Route | Purpose |
|-------|---------|
| `/v1/observations` | Keyset-paginated observations. Filters: `from`, `to`, `sensorId`, `parameterCode`, `buildingId`, `roomId`, `bbox`, `includeInvalid`, `limit`, `cursor`. |
| `/v1/observations/{id}` | A single observation (stable IRI target). |
| `/v1/observations.csv` | Streamed CSV export (F17), no page cap. |
| `/v1/context/measurements.jsonld` | The JSON-LD `@context` for observations. |
| `/v1/buildings` | Buildings. Filters: `buildingType`, `bbox`, `page`, `pageSize`. |
| `/v1/buildings/{id}` | A single building. |
| `/v1/buildings/{id}/rooms` | Rooms of a building. Filters: `roomFunction`, `minExposure` (minutes). |
| `/v1/rooms/{id}` | A single room. |
| `/v1/rooms/{id}/sensors` | Sensors in a room. Filters: `parameterCode`, `status`. |
| `/v1/sensors/{id}` | A single sensor (`sosa:Sensor`). |
| `/v1/catalog` | DCAT-AP 3.0 catalog metadata (F16). |
| `/openapi/v1.json`, `/scalar/v1` | OpenAPI document + Scalar UI (F15). |

## Content negotiation

`Accept` selects the representation: `application/json` (default),
`application/ld+json` (JSON-LD; observations reference the served `@context`,
catalog entities carry an inline context), or `text/csv` (observations only).
An explicitly unsupported type yields **406**. Every JSON/JSON-LD body carries a
`license` field; list/detail responses set `Cache-Control: public, max-age=300`
and a `Link: …; rel="describedby"` to the JSON Schema under `/v1/schema/`.

## Pagination

- **Observations** use keyset (cursor) paging on `(received_at DESC, id DESC)` so
  TimescaleDB prunes chunks and depth has no OFFSET cost. The `next` link carries
  an opaque cursor; the `(received_at, id)` tie-break guarantees no skips or dupes.
- **Catalog lists** (small sets) use simple `page`/`pageSize` offset paging.

Default page size 50, max 200 (clamped).

## Linked-data vocabularies

QUDT (quantity kinds + units, via `Core`'s `QudtVocabulary`), SSN/SOSA
(`sosa:Observation`, `sosa:Sensor`), Dublin Core / DCAT-AP for the catalog, and the
custom `ambiq:` namespace (`https://data.ambiquality.org/ns#`) for `receivedTime`
and `isInvalid`.

## Deployment note

Behind Caddy's path-stripping `handle_path /public/*`, the app no longer sees the
`/public` prefix. Set `PublicApi:BaseIri` (env `PublicApi__BaseIri`) to the public
base so the linked-data IRIs (`@id`, `next`, schema/context links) stay correct.

## Running

```bash
dotnet run --project src/Ambiquality.Public.Api   # needs the ieq + evidence DBs
dotnet test tests/Ambiquality.Public.Api.Tests    # TimescaleDB testcontainer
```

See the [root README](../../README.md) for the overall project map.
