# API reference

The platform ships an **interactive, always-current OpenAPI reference** rendered with
[Scalar](https://scalar.com) for each service. These are generated from the running code,
so they never drift from the real contract.

## Ingestion API (sending data)

> **Read-only reference.** The ingestion reference has its **"Test Request" button
> disabled** on purpose — you cannot POST real measurements from the docs UI. Submitting
> data requires a sensor key and should go through your device. Use the reference to read
> the exact schema and to copy a code sample, then send from the device. The narrative
> walkthrough is [Sending measurements](sending-measurements.md).

- **Production:** <https://data.ambiquality.org/ingestion/scalar>
- **Local dev stack (Caddy on :8080):** <http://localhost:8080/ingestion/scalar>

It documents the one endpoint you need: `POST /v1/measurements`.

## Public API (reading data)

The read side — observations (JSON / JSON-LD / CSV), the building/room/sensor catalog,
the DCAT-AP 3.0 dataset description and monthly archives — has its own reference:

- **Production:** <https://data.ambiquality.org/public/scalar>
- **Local dev stack:** <http://localhost:8080/public/scalar>

## Raw OpenAPI documents

Each Scalar page is backed by a machine-readable document you can feed to a code
generator (the URL is shown in the Scalar UI), e.g. `…/ingestion/openapi/v1.json`.
