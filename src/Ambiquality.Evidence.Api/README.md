# Ambiquality.Evidence.Api

The **evidence catalog**: registration and lifecycle management of the physical objects that
sensors measure — **buildings** and the **rooms** inside them — and of the **sensors** doing the
measuring. Covers thesis requirements **F05–F09**.

Each building, room and sensor is what the open-data catalog points at, so the service treats
their descriptive attributes as a **versioned, auditable record** rather than mutable fields.
This shapes the entire design.

The sensor is also the **canonical device registry**: its `Id` is the stable identity that
ingested measurements will reference (the planned Ingestion.Api carries `sensor_id`; there is no
separate devices table).

> **Note on the architecture.** The thesis (and `CLAUDE.md`) originally planned building/room
> registration to live in `Public.Api`. It was extracted into this dedicated service. The
> public read API and ingestion remain separate, not-yet-built projects.

## Core concept: attribute-level temporal versioning

A building's name, address, type, GPS location and construction years; a room's name, floor,
function, exposure, geometry, ventilation and pollution sources; and a sensor's identity
(manufacturer/model/serial), room placement, lifecycle status, measured-parameter
capabilities and optional installation details (F08: in-room position, distances to the nearest
window/door/pollution source, declared reporting interval, installation and last-calibration
dates) — all **change over time**, and the catalog must answer "what was true *as of* date
X", not just "what is true now".

So each mutable attribute is stored not as a column but as a stream of **history rows**, each
carrying a **validity period**:

- `Domain/Common/Validity.cs` is the *sole* factory for these periods. They are half-open,
  UTC-only `tstzrange` values — `[from, +∞)` for the currently-active value (`OpenFrom`) or
  `[from, to)` for a superseded one (`Closed`).
- Changing an attribute closes the open row at the change instant and opens a new one — the
  old value is never overwritten or deleted.
- Postgres `btree_gist` exclusion constraints (enabled in `init-databases.sql`) guarantee at
  the database level that two history rows for the same attribute can never overlap in time.

Reads accept an optional **`asOf`** query parameter (ISO-8601 UTC, default = now) to project
the aggregate's state at any past instant (parsed by `Problems.TryParseAsOf`). Mutations carry
the effective instant in the request body — `validFrom` on the attribute PUTs, `validTo` on the
collection-closing PUTs (`PUT …/pollution-sources/{code}`, `PUT …/measured-parameters/{code}`).
Invalid or non-UTC timestamps are rejected with a 400.

## Controlled codelists

Code-valued attributes are validated on write against closed codelists; an unknown code is
rejected with **400** (`UnknownCodelistCodeException`). The shared vocabularies live in
`Core.Domain.Vocabulary.Codelists` (single source of truth, also published as SKOS `číselníky`
by Public.Api): **building type**, **room function**, **ventilation type** and **pollution
source**. **Room exposure** (`Core.ExposureCode`) and **sensor status** (`SensorStatus`) were
already closed. Each concept carries parallel cs/en labels for the linked-data publication.

## Architecture

DDD layering, dependencies pointing inward (`Domain` has no framework dependencies):

```
Api/             Minimal-API endpoint groups + contracts + ProblemDetails mapping
  BuildingEndpoints.cs    /buildings ...
  RoomEndpoints.cs        /buildings/{id}/rooms ...
  SensorEndpoints.cs      /buildings/{id}/rooms/{id}/sensors ...
  Room/SensorContracts.cs request/response DTOs
  Problems.cs             DomainException -> RFC 9457 ProblemDetails, asOf/validTo parsing
Application/     Use-case handlers + ports
  Buildings/*Handler.cs   RegisterBuilding, ChangeBuilding{Name,Address,Type,Location,Years}
  Rooms/*Handler.cs       RegisterRoom, ChangeRoom{Name,Floor,Function,Exposure,Geometry,Ventilation},
                          Add/RemoveRoomPollutionSource
  Sensors/*Handler.cs     RegisterSensor, ChangeSensor{Identity,Placement,Status},
                          Add/RemoveSensorMeasuredParameter
  Abstractions/           IClock, ICurrentUser
Domain/
  Buildings/              Building aggregate + *History entities + Address/Coordinates value objects
  Rooms/                  Room aggregate + *History entities
  Sensors/                Sensor aggregate + *History entities
  Common/                 Validity, UriSlug, FloorNumber, SensorStatus, MeasuredParameter
Infrastructure/
  Persistence/            EvidenceDbContext, Building/Room/SensorRepository, EF migrations (evidence schema)
  SystemClock.cs
```

## Endpoints

Public path prefix is **`/evidence`** when accessed through the Caddy proxy (e.g.
`http://localhost:8080/evidence/buildings`); the table below shows the routes as the service
registers them. All read routes answer both `GET` and `HEAD`.

**URI slugs are server-generated, not client-supplied.** Registration ignores any slug in the
request body and assigns an opaque, globally-unique handle: a type prefix plus an 8-char
base32 token — `bld-7gk2qp` (building), `rm-k2p8wz` (room), `sns-3vh6nd` (sensor). Generation
lives in `Infrastructure/RandomSlugGenerator.cs` (behind `ISlugGenerator`); the slug is immutable
and remains the public read handle (`GET /buildings/{slug}` etc.). Because the server owns it,
registration never fails with "slug already in use". Room slugs are **globally** unique (not
per-building) — same as sensors.

### Buildings

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/buildings` | Register a building |
| `GET` `HEAD` | `/buildings/{buildingId:guid}` | Get a building by id (supports `?asOf=`) |
| `GET` `HEAD` | `/buildings/{slug}` | Get a building by URI slug (supports `?asOf=`) |
| `PUT` | `/buildings/{buildingId}/name` | Change name |
| `PUT` | `/buildings/{buildingId}/address` | Change address |
| `PUT` | `/buildings/{buildingId}/type` | Change building type |
| `PUT` | `/buildings/{buildingId}/location` | Change GPS location |
| `PUT` | `/buildings/{buildingId}/years` | Change construction/renovation years |

### Rooms (nested under a building)

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/buildings/{buildingId}/rooms` | Register a room |
| `GET` `HEAD` | `/buildings/{buildingId}/rooms/{roomId:guid}` | Get a room by id (supports `?asOf=`) |
| `GET` `HEAD` | `/buildings/{buildingId}/rooms/{slug}` | Get a room by URI slug (supports `?asOf=`) |
| `PUT` | `/buildings/{buildingId}/rooms/{roomId}/name` | Change name |
| `PUT` | `/buildings/{buildingId}/rooms/{roomId}/floor` | Change floor |
| `PUT` | `/buildings/{buildingId}/rooms/{roomId}/function` | Change function |
| `PUT` | `/buildings/{buildingId}/rooms/{roomId}/exposure` | Change exposure |
| `PUT` | `/buildings/{buildingId}/rooms/{roomId}/geometry` | Change geometry |
| `PUT` | `/buildings/{buildingId}/rooms/{roomId}/ventilation` | Change ventilation |
| `POST` | `/buildings/{buildingId}/rooms/{roomId}/pollution-sources` | Add a pollution source |
| `DELETE` | `/buildings/{buildingId}/rooms/{roomId}/pollution-sources/{sourceCode}` | Remove a pollution source |

### Sensors (nested under a room)

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/buildings/{buildingId}/rooms/{roomId}/sensors` | Register a sensor in a room |
| `GET` `HEAD` | `…/rooms/{roomId}/sensors/{sensorId:guid}` | Get a sensor by id (supports `?asOf=`) |
| `GET` `HEAD` | `…/rooms/{roomId}/sensors/{slug}` | Get a sensor by URI slug (supports `?asOf=`) |
| `PUT` | `…/sensors/{sensorId}/identity` | Change manufacturer / model / serial |
| `PUT` | `…/sensors/{sensorId}/placement` | Relocate the sensor (body: `newRoomId`) |
| `PUT` | `…/sensors/{sensorId}/status` | Change lifecycle status (codelist) |
| `PUT` | `…/sensors/{sensorId}/installation` | Record installation details (F08; all fields optional) |
| `POST` | `…/sensors/{sensorId}/measured-parameters` | Add a measured-parameter capability |
| `DELETE` | `…/sensors/{sensorId}/measured-parameters/{parameterCode}` | Remove a measured-parameter capability |

**Nested URL, but a movable, stable sensor.** A sensor is registered and read *under* its room
(matching how rooms nest under buildings), yet it has a stable `Id`/slug and its room is a
**versioned placement**, so it can be relocated. The reconciliation:

- *Reads* (`GET`/`HEAD`) resolve the sensor and return `404` unless it was placed in the path's
  room (and building) **as of** the requested instant — so the nested URL stays meaningful even
  across moves.
- *Mutations* address the sensor by `Id`; the `{buildingId}`/`{roomId}` path segments locate it
  but are not re-validated (mirroring the room change endpoints).
- A **move** is `PUT …/sensors/{id}/placement` carrying the destination `newRoomId` (the
  building is derived from the room). The sensor's denormalised current room/building update and
  a new placement-history row opens.

The slug is **globally unique** (not per-room), since the sensor's identity is stable.

**Per-sensor API key.** Registration generates a secret key (`amq_sk_…`) returned **once** in the
`POST` response (`apiKey`) and never again — only its SHA-256 hash is stored (`sensors.api_key_hash`).
The planned Ingestion service authenticates each sensor by hashing the presented key and comparing
against this hash. SHA-256 (not a password KDF) is deliberate: keys are high-entropy random values,
so one fast hash is safe and keeps ingestion verification cheap under the ≥100 msg/s target.

Status codes (`sensor_status`): `active`, `maintenance`, `decommissioned`. Measured parameters
(`measured_parameter`): `co2`, `temperature`, `humidity`, `pm`, `voc`, `acoustics`, `light`. An
unknown code is a `400 unknown-codelist-code`.

**Optional installation details (F08).** A sensor may carry an `installation` object — its
in-room `positionNote`, the distances to the nearest window/door/pollution source
(`distanceWindowM`/`distanceDoorM`/`distanceSourceM`, metres), the declared reporting interval
(`measurementFrequencySeconds`), and the `installedOn`/`lastCalibratedOn` dates. Every field is
optional and the attribute as a whole is optional: a sensor may have no installation row at all,
in which case reads project `installation: null`. The block can be supplied at registration
(nested in the `POST` body) and changed later (`PUT …/sensors/{id}/installation` with the same
fields plus `validFrom`), which closes the open row half-open and opens a new one — the same
temporal versioning as every other sensor attribute. Validation: distances and frequency must be
positive when provided, and `lastCalibratedOn` must not precede `installedOn`; violations are a
`400 domain-rule-violation`.

`HEAD` is handled by middleware in `Program.cs` that runs the matching `GET` handler and
discards the body while preserving status and headers (RFC 9110 §9.3.2).

### Address lookup (RÚIAN autocomplete)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/address-lookup/suggest?q=&limit=` | Autocomplete Czech addresses (`{ suggestions: [{ text, key }] }`) |
| `GET` | `/address-lookup/resolve?key=` | Expand a suggestion `key` to the full OFN address (structured components + RÚIAN codes + WGS84 coordinates + composed `text`) |

A convenience for the building-registration form so the registrar **picks** an address instead of
hand-copying the ~18 OFN `Adresy` fields out of the registry. Both endpoints **require
authorization** (it is not an open geocoding proxy), and the resolved address is still
re-validated by `RegisterBuilding` — this never bypasses the domain rules.

Backed by the **ČÚZK RÚIAN ArcGIS** service (`Ruian:BaseUrl`, default
`https://ags.cuzk.gov.cz/arcgis/rest/services/RUIAN/MapServer/`). `suggest` calls the Esri
`GeocodeSOE` locator (address points only); `resolve` reads the `AdresniMisto` layer for the
suggestion's object id, then enriches it from the street and territorial layers
(`Ulice` / `Obec` / `CastObce` / `Okres` / `VyssiUzemneSamospravnyCelek`) in parallel. The
integration lives in `Infrastructure/Ruian/RuianGeocoderClient.cs` behind `IAddressGeocoder`.

> **Attribution.** RÚIAN data and services are open data under **CC BY 4.0** — © ČÚZK. The
> consuming UI must credit ČÚZK/RÚIAN. No API key or registration is required; the client uses a
> 5 s timeout and the endpoints answer **502 `address-lookup-unavailable`** (not 500) when the
> upstream service fails, so a caller can fall back to manual entry.

### HTTP verb & status convention

The API follows one rule consistently across buildings and rooms:

| Operation kind | Verb | Success | Example |
|----------------|------|---------|---------|
| Replace one attribute | `PUT` | `204 No Content` | `PUT /buildings/{id}/name`, `PUT …/rooms/{id}/floor` |
| Register an aggregate | `POST` | `201 Created` (+ `Location`) | `POST /buildings`, `POST …/rooms` |
| Add an item to a collection | `POST` | `200 OK` | `POST …/rooms/{id}/pollution-sources` |
| Remove an item from a collection | `DELETE` | `200 OK` | `DELETE …/rooms/{id}/pollution-sources/{code}` |

**Why `PUT` for attribute changes.** Each attribute is addressed by its own URI
(`…/{id}/name`, `…/{id}/floor`), and the request body carries that attribute's *complete* new
value plus a `validFrom` instant — i.e. the full representation of that single-attribute
sub-resource. That is exactly what `PUT` means (RFC 9110 §9.3.4: "replace the target resource
with the enclosed representation"), so `PUT` reads more naturally than `PATCH` here. `PATCH`
would imply the body is a partial *patch document* (JSON Patch / Merge Patch); it is not, so
`PUT` is the better fit. No body is returned, hence `204`.

Pollution sources are a *collection* on the room, not a single attribute, so they use
`POST` (add) / `DELETE` (remove) rather than `PUT`.

**Idempotency.** `PUT` is defined to be idempotent (RFC 9110 §9.3.4). Replaying the *same*
single-attribute `PUT` — identical new value **and** identical `validFrom` as the current open
history row — is a silent no-op: each `Change*` method short-circuits and returns before
appending, so no new history row is written and the endpoint still answers `204`. This makes a
client retry after a dropped response safe. Two cases remain genuine conflicts and still return
`400 domain-rule-violation`: a `validFrom` equal to the current row's start but with a
*different* value, and any `validFrom` at or before the current row's start. (Sending the same
value at a *strictly later* `validFrom` is not a replay — it appends a new row, as intended.)

Note this idempotency applies to single-attribute `PUT`s only. The `409 overlapping-validity-range`
seen in the error table below is reachable for *collection* re-adds (pollution sources, measured
parameters), which use `POST`/`DELETE` and have no domain-level no-op — making those idempotent is
a separate, out-of-scope follow-up.

The interactive OpenAPI reference (Scalar) and raw document are exposed **only in Development**
at `/scalar/v1` and `/openapi/v1.json` — directly at <http://localhost:6200/scalar/v1>.

## Error model

`Problems.cs` maps domain exceptions to RFC 9457 ProblemDetails with stable
`urn:ambiquality:evidence:*` type URIs:

| Condition | Status | Type URN suffix |
|-----------|--------|-----------------|
| Building / room / sensor not found | `404` | `building-not-found`, `room-not-found`, `sensor-not-found` |
| Pollution source / measured parameter not found | `404` | `pollution-source-not-found`, `measured-parameter-not-found` |
| Not the owner | `403` | `forbidden` |
| Slug already taken (DB race safety net; unreachable on the create path now slugs are server-generated) | `409` | `duplicate-uri-slug` |
| Validity period overlaps an existing one | `409` | `overlapping-validity-range` |
| Code not in the relevant codelist | `400` | `unknown-codelist-code` |
| Any other domain-rule violation (bad valid-from, empty value, non-UTC timestamp) | `400` | `domain-rule-violation` |
| Missing open history row (data corruption — should be impossible) | `500` | `internal-server-error` |

## Temporal integrity

All three aggregates — buildings, rooms and sensors — enforce the no-overlap invariant **end-to-end**
through the change path, on two complementary mechanisms:

- **Half-open closing.** Closing a history row writes a `[lower, validFrom)` range with an
  **exclusive** upper bound, so the closed row and the next open row (which starts at
  `validFrom`) do not both contain the boundary instant. Every aggregate's `Close` routes
  through the `Validity.Closed` factory (or the equivalent explicit half-open `NpgsqlRange`
  ctor for sensors); none uses the raw two-argument `NpgsqlRange` constructor, which would
  produce an *inclusive* upper bound `[lower, validFrom]` that overlaps the next row.
- **Deferred GiST constraints.** A change closes the open row (UPDATE) and opens a new one
  (INSERT) in one transaction, and EF may emit the INSERT first. The `btree_gist` exclusion
  constraints are `DEFERRABLE INITIALLY DEFERRED` so the no-overlap check runs at COMMIT, after
  both rows have settled. Single-value attributes exclude on `(<id>, validity)`; collections add
  the item code — `(sensor_id, parameter_code, validity)` for measured parameters and
  `(room_id, source_code, validity)` for room pollution sources.

## Authentication & authorization

The service validates the **HMAC-SHA256 JWT access tokens issued by Auth.Api** (it shares the
`Jwt` issuer / audience / signing secret, but only validates — it never issues). Bearer auth is
wired in `Program.cs`; `MapInboundClaims` is disabled so the raw `sub` claim survives.

- **Identity (`UserProjection`).** The catalog never stores the raw auth `sub` GUID on its rows.
  `CurrentUserMiddleware` runs after authentication and, for any authenticated request, lazily
  upserts a local `evidence.user_projections` row keyed by the unique `auth_user_id` (the `sub`).
  `ICurrentUser` then exposes `AuthUserId` (the `sub`) and `ProjectionId` (the local row id);
  ownership and audit columns (`OwnerId`, `recorded_by`) store the `ProjectionId`. The upsert
  tolerates a concurrent first-request race via the unique index.
- **Mutations require a token.** Every `POST` / `PUT` / `DELETE` is `RequireAuthorization()`
  (group-level); an unauthenticated mutation gets `401`. Reads (`GET` / `HEAD`) are
  `AllowAnonymous` — the catalog is open data — but authentication still runs so an owner can be
  recognised on read.
- **Ownership.** A building's `OwnerId` is its registrar. `BuildingAuthorizer` rejects non-owners
  with `403 forbidden`. Rooms and sensors have no owner of their own — ownership derives from the
  containing building, enforced by `RoomAuthorizer` / `SensorAuthorizer` (a sensor move checks
  both the source and destination building).
- **Czech OFN addresses.** A building's address follows the Czech OFN *Adresy* (2020-07-01)
  standard, anchored on the RÚIAN address-point code (`address_point_code`) with the structured
  components stored alongside (`street_name`, `house_number` + `house_number_type` č.p./č.ev.,
  `orientation_number`, `municipality_name`, `municipality_part_name`, `psc`, plus optional
  `district_name`/`region_name`). OFN models the territorial elements as dereferenceable RÚIAN
  IRIs, so the optional RÚIAN codes that back them are stored too (`street_code`,
  `municipality_code`, `municipality_part_code`, `district_code`, `region_code`) — Public.Api emits
  the `ulice`/`obec`/`část_obce`/`okres`/`vúsc` IRIs from them. The platform is Czech-only, so there
  is no country field.
- **Coordinates are open data.** Latitude/longitude are stored and returned precisely to every
  reader — there is no anonymization (publishing precise building coordinates is noted as a privacy
  risk in the thesis, not implemented as a mitigation here).

The interactive OpenAPI reference advertises the `Bearer` scheme; mutation operations carry the
security requirement, reads do not.

## Known gaps

- **Idempotent collection re-adds.** Single-attribute `PUT`s are now idempotent (an identical
  re-PUT returns `204`; see *Idempotency* above). Collection operations (pollution sources,
  measured parameters) use `POST`/`DELETE` and still surface `409 overlapping-validity-range` on a
  duplicate re-add; making those a silent no-op is the remaining follow-up.

## Database & migrations

Owns the **`evidence`** database (schema `evidence`) via `EvidenceDbContext`, requiring the
`btree_gist` extension (created in `init-databases.sql`). Migrations are applied automatically
at startup by the `evidence-migrate` container before `evidence-api` starts.

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Ambiquality.Evidence.Api \
  --startup-project src/Ambiquality.Evidence.Api
```

Then rebuild: `./dev-build.sh`.

## Running & testing

```bash
dotnet run --project src/Ambiquality.Evidence.Api      # run standalone
dotnet test tests/Ambiquality.Evidence.Api.Tests       # run this project's tests
```

See the [root README](../../README.md) for full container setup.
