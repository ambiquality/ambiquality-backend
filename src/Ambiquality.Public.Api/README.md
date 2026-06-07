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
| `/v1/observations/aggregate` | Server-side aggregation (F18 map chart): bucketed trend + distribution. Requires `buildingId` *or* `sensorId`, plus `parameterCode`; filters `from`, `to`, `bucket` (`auto`/`5m`/`1h`/`6h`/`1d`/`1w`). JSON only. |
| `/v1/observations.csv` | Streamed CSV export (F17), no page cap. |
| `/v1/map/snapshot` | Latest-value building markers for the F18 map. Requires `parameterCode`; filters `bbox`. One item per building with ≥1 active sensor measuring the quantity; `latestValue` is the mean of each sensor's most-recent value, `null` when stale. Redis-cached ~60 s. JSON only. |
| `/v1/context/measurements.jsonld` | The JSON-LD `@context` for observations. |
| `/v1/properties` | The IEQ observable-property vocabulary (all 18). |
| `/v1/properties/{code}` | A single observable property — the `sosa:observedProperty` target. |
| `/v1/codelists` | The catalog codelists as SKOS concept schemes. |
| `/v1/codelists/{scheme}` | A single codelist (e.g. `building-type`). |
| `/v1/codelists/{scheme}/{code}` | A single codelist concept — the dereferenceable IRI target. |
| `/v1/buildings` | Buildings. Filters: `buildingType`, `bbox`, `page`, `pageSize`. |
| `/v1/buildings/{id}` | A single building. |
| `/v1/buildings/{id}/rooms` | Rooms of a building. Filters: `roomFunction`, `minExposure` (minutes). |
| `/v1/rooms/{id}` | A single room. |
| `/v1/rooms/{id}/sensors` | Sensors in a room. Filters: `parameterCode`, `status`. |
| `/v1/sensors/{id}` | A single sensor (`sosa:Sensor`). |
| `/v1/catalog` | DCAT-AP 3.0 catalog metadata (F16). |
| `/openapi/v1.json`, `/scalar/v1` | OpenAPI document + Scalar UI (F15). |

## OpenAPI document (F15)

The spec is a published deliverable, served in **every** environment (it is open
metadata, not a dev-only convenience). A document transformer stamps the
document-level `info` — title, version, a description, and the **CC BY 4.0**
license (matching the `license` on every response body) — and a `servers` entry
so Scalar "Try it" and any generated client target the real deployment. Behind
Caddy (`handle_path /public/*` strips the prefix) the operator sets
`PublicApi:BaseIri` to the external versioned root (e.g.
`https://data.ambiquality.org/v1`); the transformer strips the trailing `/v1`
(the document paths already carry it) and advertises the remaining origin as the
server URL. With `PublicApi:BaseIri` unset (local dev) no `servers` entry is
emitted and clients fall back to the request origin. There is no security scheme —
the API is unauthenticated by design.

## Content negotiation

`Accept` selects the representation: `application/json` (default),
`application/ld+json` (JSON-LD; observations reference the served `@context`,
catalog entities carry an inline context), or `text/csv` (observations only).
An explicitly unsupported type yields **406**. Every JSON/JSON-LD body carries a
`license` field; list/detail responses set `Cache-Control: public, max-age=300`
and a `Link: …; rel="describedby"` to the JSON Schema under `/v1/schema/`. The CSV
export carries the same `describedby` link, pointing instead at the **CSVW** tabular
schema `/v1/schema/observations.csv-metadata.json` — it names every column, its
datatype, and the SSN/SOSA + QUDT property each maps to, so the CSV lifts into the
same RDF model as the JSON-LD. The DCAT catalog advertises this schema on every CSV
distribution (live + monthly archives) via `dcterms:conformsTo`. The export worker's
monthly CSV archives share this exact column schema.

## Pagination

- **Observations** use keyset (cursor) paging on `(received_at DESC, id DESC)` so
  TimescaleDB prunes chunks and depth has no OFFSET cost. The `next` link carries
  an opaque cursor; the `(received_at, id)` tie-break guarantees no skips or dupes.
- **Catalog lists** (small sets) use simple `page`/`pageSize` offset paging.

Default page size 50, max 200 (clamped).

## Observable properties (`sosa:observedProperty`)

A measurement's `sosa:observedProperty` must identify **what substance/quantity**
was observed. A QUDT *quantity kind* is the wrong granularity for that: it
describes only the physical **dimension**, so `pm1`/`pm2_5`/`pm4`/`pm10` all share
`quantitykind:MassDensity` and `voc`/`co`/`co2`/`eco2` all share
`quantitykind:AmountOfSubstanceFraction` — a consumer could not tell PM2.5 from
PM10. So each parameter gets a **specific, dereferenceable** property IRI under
`/v1/properties/{code}` (`Core`'s `ObservablePropertyVocabulary`), exposed as a
`sosa:ObservableProperty` / `skos:Concept` that carries:

- `qudt:hasQuantityKind` — the QUDT dimensional kind (the value that *used* to be
  mis-published as `observedProperty`),
- `qudt:applicableUnit` — the canonical QUDT unit,
- `skos:exactMatch` to the authoritative **EEA/EIONET air-quality pollutant** code
  where one exists (`pm2_5`→6001, `pm10`→5, `o3`→7, `no2`→8, `so2`→1, `co`→10).

On each observation, `sosa:observedProperty` is the specific IRI and
`qudt:hasQuantityKind` carries the dimensional kind; the CSV/CSVW export mirrors
this with an `observed_property_uri` column (`sosa:observedProperty`) alongside
`quantity_kind_uri` (`qudt:hasQuantityKind`).

## Codelists (SKOS `číselníky`)

The catalog's code-valued attributes — building type, room function, ventilation type,
pollution source, room exposure and sensor status — are published as **SKOS concept
schemes** at `/v1/codelists`. Each concept is dereferenceable
(`/v1/codelists/{scheme}/{code}`), typed `skos:Concept`, and carries a `skos:notation`
plus cs + en `skos:prefLabel`s. The catalog entities reference these concepts: a
building's `ambiq:buildingType` (and the room/sensor code attributes) is a
`{ "@id": ".../codelists/building-type/office", "skos:notation": "office" }` reference
rather than a bare string. The controlled vocabularies are defined once in
`Core.Domain.Vocabulary.Codelists` and validated on write by Evidence.Api. A legacy value
outside its codelist degrades gracefully to a bare string.

## Feature of interest (`sosa:hasFeatureOfInterest`)

Each observation links to the **room it was measured in** via `sosa:hasFeatureOfInterest`
(`featureOfInterestIri` in plain JSON). Because sensor placement is temporally versioned,
the room is resolved at the observation's **observation time** — a measurement taken before
a sensor was relocated still points at the room it was actually in then, not the sensor's
current room. The room is omitted when no placement period covers the observation (e.g. data
predating the first placement record). This is emitted in JSON and JSON-LD; the CSV export
keeps its fixed CSVW column set and does not carry it.

## Catalog conformance (DCAT-AP 3.0 / DCAT-AP-CZ)

`/v1/catalog` is structurally **DCAT-AP 3.0** and is **partially aligned** with the
Czech **DCAT-AP-CZ / OFN** profile. What the document carries:

- cs + en language-tagged `dcterms:title` and `dcterms:description` on both the
  Catalog and the Dataset (DCAT-AP-CZ requires the multilingual literals);
- `dcterms:publisher` on the **Catalog** (mandatory in base DCAT-AP 3.0) as well as
  the Dataset, plus a `dcat:contactPoint`;
- `dcat:theme` (EU data-theme `ENVI`), `dcat:keyword` (cs + en),
  `dcterms:accrualPeriodicity` (EU frequency `CONT`), and `dcterms:format` (EU
  file-type codelist) on every distribution alongside `dcat:mediaType`.

**What it cannot meet — and why.** Full DCAT-AP-CZ conformance requires
`dcterms:publisher` to be an **IRI from the Czech OVM/RPP register** (*orgán veřejné
moci* — a public authority). This project is authored by an individual student, who
is **not** an OVM; the national open-data coordinator confirmed by e-mail that no such
publisher identity can be assigned. The publisher is therefore a free-text
`foaf:Agent`, and the catalogue is **DCAT-AP-CZ-aligned only in part**. This is a
structural limitation of the thesis context, not an implementation gap. The catalog code
attributes are now published as dereferenceable SKOS `číselníky` (see *Codelists* above);
RÚIAN spatial IRIs remain out of scope.

## Linked-data vocabularies

QUDT (quantity kinds + units, via `Core`'s `QudtVocabulary`), SSN/SOSA
(`sosa:Observation`, `sosa:Sensor`, `sosa:ObservableProperty`), SKOS for the
property vocabulary, Dublin Core / DCAT-AP for the catalog, and the custom
`ambiq:` namespace (`https://data.ambiquality.org/ns#`) for `receivedTime`
and `isInvalid`.

## Deployment note

Behind Caddy's path-stripping `handle_path /public/*`, the app no longer sees the
`/public` prefix. Set `PublicApi:BaseIri` (env `PublicApi__BaseIri`) to the public
base so the linked-data IRIs (`@id`, `next`, schema/context links) stay correct.

`/v1/map/snapshot` caches its result for ~60 s in Redis when `ConnectionStrings:Redis`
(env `ConnectionStrings__Redis`) is set; with no Redis configured it falls back to an
in-process memory cache, and any cache fault degrades to a fresh compute (the read path
never fails because of the cache). The snapshot's freshness window — beyond which a
marker's `latestValue` is nulled and `stale` is set — defaults to 15 min and is
overridable via `PublicApi:MapSnapshotFreshnessSeconds`.

## Running

```bash
dotnet run --project src/Ambiquality.Public.Api   # needs the ieq + evidence DBs
dotnet test tests/Ambiquality.Public.Api.Tests    # TimescaleDB testcontainer
```

See the [root README](../../README.md) for the overall project map.
