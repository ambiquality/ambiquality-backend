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
(manufacturer/model/serial), room placement, lifecycle status and measured-parameter
capabilities — all **change over time**, and the catalog must answer "what was true *as of* date
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
the aggregate's state at any past instant. Mutations accept an optional **`validTo`** /
valid-from to control the effective instant of the change. Invalid or non-UTC timestamps are
rejected with a 400 (`Problems.TryParseAsOf` / `TryParseValidTo`).

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
  Common/                 Validity, UriSlug, FloorNumber, AnonymizationLevel, SensorStatus, MeasuredParameter
Infrastructure/
  Persistence/            EvidenceDbContext, Building/Room/SensorRepository, EF migrations (evidence schema)
  SystemClock.cs
```

## Endpoints

Public path prefix is **`/evidence`** when accessed through the Caddy proxy (e.g.
`http://localhost:8080/evidence/buildings`); the table below shows the routes as the service
registers them. All read routes answer both `GET` and `HEAD`.

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

Status codes (`sensor_status`): `active`, `maintenance`, `decommissioned`. Measured parameters
(`measured_parameter`): `co2`, `temperature`, `humidity`, `pm`, `voc`, `acoustics`, `light`. An
unknown code is a `400 unknown-codelist-code`.

`HEAD` is handled by middleware in `Program.cs` that runs the matching `GET` handler and
discards the body while preserving status and headers (RFC 9110 §9.3.2).

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

**Idempotency caveat (known limitation).** `PUT` is defined to be idempotent. Because the
store is append-only temporal, replaying the *same* `PUT` with an identical `validFrom`
currently fails with `409 overlapping-validity-range` instead of being a silent no-op. The
state still ends up correct, but a strictly idempotent `PUT` would treat the duplicate as a
`204` no-op. Making the open-history rows idempotent on identical re-PUT is tracked as a
follow-up (it touches the aggregates and the `btree_gist` invariant, so it needs its own tests).

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
| Slug already taken | `409` | `duplicate-uri-slug` |
| Validity period overlaps an existing one | `409` | `overlapping-validity-range` |
| Code not in the relevant codelist | `400` | `unknown-codelist-code` |
| Any other domain-rule violation (bad valid-from, empty value, non-UTC timestamp) | `400` | `domain-rule-violation` |
| Missing open history row (data corruption — should be impossible) | `500` | `internal-server-error` |

## Temporal integrity (sensors)

The sensor history tables are the first to enforce the no-overlap invariant **end-to-end**
through the change path, which surfaced two subtleties the building/room code never exercised:

- **Half-open closing.** Closing a history row writes a `[lower, validFrom)` range with an
  **exclusive** upper bound, so the closed row and the next open row (which starts at
  `validFrom`) do not both contain the boundary instant. The raw two-argument `NpgsqlRange`
  constructor produces an *inclusive* upper bound — the building/room `Close` methods use it and
  therefore write `[lower, validFrom]`; they get away with it only because rooms have no
  exclusion constraint and building change handlers are unit-tested against an in-memory repo.
- **Deferred GiST constraints.** A change closes the open row (UPDATE) and opens a new one
  (INSERT) in one transaction, and EF may emit the INSERT first. The `btree_gist` exclusion
  constraints are `DEFERRABLE INITIALLY DEFERRED` so the no-overlap check runs at COMMIT, after
  both rows have settled. Single-value attributes exclude on `(sensor_id, validity)`; the
  measured-parameter collection excludes on `(sensor_id, parameter_code, validity)`.

## Known gaps

- **Authentication is stubbed.** `CurrentUserStub` in `Program.cs` returns a hardcoded user
  GUID; there is no JWT validation yet. Ownership checks (`BuildingAuthorizer`,
  `ForbiddenException`) run against that stub. Wiring real JWT bearer auth (as Auth.Api issues)
  and extracting the `sub` claim into `ICurrentUser` is outstanding. Sensor mutations, like room
  mutations, currently perform no ownership check.
- **Building/room history close is inclusive-upper** (see *Temporal integrity* above) and the
  room history tables have **no GiST exclusion constraints** at all — both latent, masked by the
  current tests. Worth aligning with the sensor approach when auth/idempotency are revisited.

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
