# Entity-relationship diagrams

The three databases (one Postgres instance, three schemas) and how they relate. Diagrams are
[Mermaid](https://mermaid.js.org/) `erDiagram`s rendered by the Git host and derived from the
EF Core models / migrations — **not** scaffolded from a live database.

A deliberate constraint runs through all three: **there are no cross-database foreign keys.**
User identity travels in the JWT `sub` claim (projected into `evidence.user_projections`); a
measurement's `sensor_id` references the evidence catalog logically, with no FK. Dashed notes
below mark these soft boundaries.

---

## `auth` — Auth.Api

Users and their hashed credentials, with refresh and verification tokens as owned collections.

```mermaid
erDiagram
    users ||--o{ refresh_tokens : has
    users ||--o{ verification_tokens : has

    users {
        uuid id PK
        varchar email UK
        bool email_confirmed
        text password_hash
        varchar pending_email "nullable — pending email change"
        int failed_login_count "throttling, not lockout"
        timestamptz last_failed_login_at "nullable"
    }
    refresh_tokens {
        uuid id PK
        uuid user_id FK
        text token_hash
        timestamptz created_at
        timestamptz expires_at
        timestamptz revoked_at "nullable"
    }
    verification_tokens {
        uuid id PK
        uuid user_id FK
        text token_hash
        int purpose "email-confirm / password-reset / email-change"
        timestamptz created_at
        timestamptz expires_at
        timestamptz consumed_at "nullable — single-use"
    }
```

---

## `evidence` — Evidence.Api

Buildings, rooms and sensors are thin aggregate roots; **every attribute is a stream of
temporal history rows**, not a mutable column. Each history row carries a half-open UTC
`tstzrange` **`validity`** plus `recorded_at` / `recorded_by` audit columns, and its primary
key is `(<owner_id>, recorded_at)` (collections add the item code). `btree_gist` exclusion
constraints forbid overlapping validity for the same attribute. To keep the diagrams legible,
the shared `validity` / `recorded_at` / `recorded_by` columns are omitted from the history
entities below and only the payload is shown.

### Aggregate roots

```mermaid
erDiagram
    user_projections ||--o{ buildings : owns
    buildings ||--o{ rooms : contains
    buildings ||--o{ sensors : "current building"
    rooms ||--o{ sensors : "current room"

    user_projections {
        uuid id PK
        uuid auth_user_id "= JWT sub; no FK to auth.users"
        timestamptz created_at
    }
    buildings {
        uuid id PK
        text uri_slug UK
        uuid owner_id FK
        uuid created_by FK
        timestamptz created_at
    }
    rooms {
        uuid id PK
        uuid building_id FK
        text uri_slug UK
        uuid created_by FK
        timestamptz created_at
    }
    sensors {
        uuid id PK
        text uri_slug UK
        uuid current_building_id FK
        uuid current_room_id FK
        text api_key_hash
        uuid created_by FK
        timestamptz created_at
    }
```

### Building attribute history (PK `(building_id, recorded_at)`)

```mermaid
erDiagram
    buildings ||--o{ building_name_history : ""
    buildings ||--o{ building_address_history : ""
    buildings ||--o{ building_type_history : ""
    buildings ||--o{ building_location_history : ""
    buildings ||--o{ building_years_history : ""

    building_name_history {
        uuid building_id FK
        text name
    }
    building_address_history {
        bigint address_point_code "RÚIAN (OFN Adresy)"
        text street_name "nullable"
        int house_number
        text house_number_type "č.p./č.ev."
        int orientation_number "nullable"
        text municipality_name
        text municipality_part_name "nullable"
        text psc
        text district_name "nullable, okres"
        text region_name "nullable, kraj"
    }
    building_type_history {
        uuid building_id FK
        text building_type_code "codelist"
    }
    building_location_history {
        uuid building_id FK
        float8 latitude
        float8 longitude
    }
    building_years_history {
        uuid building_id FK
        int year_built
        int year_renovated
    }
```

### Room attribute history (PK `(room_id, recorded_at)`)

```mermaid
erDiagram
    rooms ||--o{ room_name_history : ""
    rooms ||--o{ room_floor_history : ""
    rooms ||--o{ room_function_history : ""
    rooms ||--o{ room_exposure_history : ""
    rooms ||--o{ room_geometry_history : ""
    rooms ||--o{ room_ventilation_history : ""
    rooms ||--o{ room_pollution_source_history : ""

    room_name_history {
        uuid room_id FK
        text name
    }
    room_floor_history {
        uuid room_id FK
        smallint floor
    }
    room_function_history {
        uuid room_id FK
        text function_code "codelist"
    }
    room_exposure_history {
        uuid room_id FK
        text exposure_code "codelist: short/medium/long"
    }
    room_geometry_history {
        uuid room_id FK
        float8 area_m2
        float8 ceiling_height_m
    }
    room_ventilation_history {
        uuid room_id FK
        text ventilation_type "codelist"
    }
    room_pollution_source_history {
        uuid room_id FK
        text source_code "codelist; PK includes source_code"
    }
```

### Sensor attribute history (PK `(sensor_id, recorded_at)`)

```mermaid
erDiagram
    sensors ||--o{ sensor_identity_history : ""
    sensors ||--o{ sensor_status_history : ""
    sensors ||--o{ sensor_placement_history : ""
    sensors ||--o{ sensor_measured_parameter_history : ""

    sensor_identity_history {
        uuid sensor_id FK
        text manufacturer
        text model
        text serial_number
    }
    sensor_status_history {
        uuid sensor_id FK
        text status_code "codelist: active/maintenance/decommissioned"
    }
    sensor_placement_history {
        uuid sensor_id FK
        uuid building_id
        uuid room_id "room at a point in time -> feature of interest"
    }
    sensor_measured_parameter_history {
        uuid sensor_id FK
        text parameter_code "PK includes parameter_code"
    }
```

---

## `ieq` — TimescaleDB (owned by Ingestion.Api migrations)

The `measurements` **hypertable** (partitioned on `received_at`, composite key
`(id, received_at)`), the `parameter_ranges` codelist (18 seeded IEQ parameters), and the
`measurement_exports` log written by Export.Worker.

```mermaid
erDiagram
    measurements {
        uuid id PK "generated by the API at enqueue"
        timestamptz received_at PK "API-stamped; hypertable partition key"
        timestamptz observed_at "API-stamped at acceptance (= received_at); sensor clock untrusted"
        uuid sensor_id "-> evidence.sensors (no cross-db FK)"
        varchar parameter_code "-> parameter_ranges (logical)"
        float8 value
        varchar unit
        bool is_invalid "soft-invalidation; never DELETE/UPDATE the value"
        varchar invalidated_reason "nullable"
    }
    parameter_ranges {
        varchar parameter_code PK
        float8 min_value
        float8 max_value
        varchar unit
    }
    measurement_exports {
        uuid id PK
        smallint year "UK (year, month, media_type)"
        smallint month
        varchar media_type
        varchar compress_format
        varchar file_key
        varchar download_url
        bigint file_size_bytes "nullable"
        bigint record_count "nullable"
        timestamptz exported_at
    }
```

**Cross-schema soft references (no FK):**

- `ieq.measurements.sensor_id` ⇢ `evidence.sensors.id` — validated by Ingestion.Api against the
  evidence catalog over a read-only connection at acceptance; never enforced by a constraint.
- `ieq.measurements.parameter_code` ⇢ `ieq.parameter_ranges.parameter_code` — same schema, but
  range-checked in application code, not by an FK.
