# How the data is published

Once your measurements are accepted they become **open data**. Here is where they end up
and how a consumer reaches them — all under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).

## Live observations

The **Public API** serves observations with filtering and keyset pagination, in three
representations via content negotiation:

- `application/json` — plain JSON,
- `application/ld+json` — JSON-LD (linked data, SOSA/SSN + QUDT vocabularies),
- `text/csv` — CSV with a CSVW tabular schema.

Browse it in the [Public API reference](api-reference.md).

## DCAT-AP catalogue

`GET /public/v1/catalog` returns a [DCAT-AP 3.0](https://semiceu.github.io/DCAT-AP/)
description of the platform's data:

- a continuous **live dataset** (the queryable API above), and
- a **`dcat:DatasetSeries`** whose members are one dataset **per calendar month**, each
  carrying its downloadable archive distributions.

## Monthly archives

For bulk download, the platform publishes **one gzip-compressed file per calendar month
per format** (CSV and JSON-LD) — a single file, never a multi-file zip container. Each is
listed as a `dcat:Distribution` of that month's dataset in the catalogue, with its byte
size and record count.

## Immutability

Published measurements are **never silently changed or deleted**. A reading later found to
be wrong is flagged invalid (and excluded from default reads), but the historical record
is preserved. This is why the ingestion path validates strictly up front — see
[Sending measurements](sending-measurements.md).
