# Ambiquality.Export.Worker

Background worker that publishes monthly open-data archives of the measurement
hypertable. Once per month it exports the most recent fully-elapsed calendar month to
S3-compatible object storage in two formats — **CSV** and **JSON-LD**, each zipped —
and records the export in `ieq.measurement_exports` so the DCAT-AP catalog in
**Public.Api** can list them as downloadable `dcat:Distribution` entries (F16/F17).

## How it works

1. **Schedule** — `MonthlyExportService` (a `BackgroundService`) wakes, determines the
   most recent fully-elapsed UTC calendar month, and checks which formats are already
   recorded for it. It exports the missing ones, then sleeps until **02:00 UTC on the
   first of the next month**. A failed pass backs off one hour and retries (the month
   is not skipped).
2. **Export** — `MonthlyExporter` streams the month's rows from the hypertable
   (`received_at` in a half-open `[start, nextMonthStart)` window, the partition
   column) one row at a time through the format serializer into a `ZipArchive` entry.
   The archive is staged in a temp file (not memory), uploaded, then deleted.
3. **Record** — the upload's download URL, byte size and record count land in
   `ieq.measurement_exports` (`ON CONFLICT (year, month, media_type) DO NOTHING`, so a
   redelivered pass is idempotent).

No EF Core: all DB access is raw Npgsql (`ExportRepository`), the same pattern as
Ingestion.Worker. The `MeasurementExport` entity and its migration are owned by
Ingestion.Api's `IeqDbContext` migrations pipeline.

## Storage

`IExportStorage` has two implementations selected by `Export:StorageType`:

- `S3ExportStorage` — `AWSSDK.S3` against a configurable `ServiceUrl` (Hetzner Object
  Storage); the public download URL is built from `PublicBaseUrl + key`.
- `FileSystemExportStorage` — writes under a local base path (dev / tests).

Object key layout:

```
exports/{year:D4}/{month:D2}/measurements-{year:D4}-{month:D2}.csv.zip
exports/{year:D4}/{month:D2}/measurements-{year:D4}-{month:D2}.jsonld.zip
```

## Configuration (`Export` section)

```json
{
  "Export": {
    "StorageType": "S3",
    "BaseIri": "https://data.ambiquality.org",
    "S3": {
      "ServiceUrl": "https://fsn1.your-objectstorage.com",
      "BucketName": "ambiquality-exports",
      "AccessKey": "...",
      "SecretKey": "...",
      "PublicBaseUrl": "https://fsn1.your-objectstorage.com/ambiquality-exports"
    },
    "FileSystem": { "BasePath": "/exports", "PublicBaseUrl": "" }
  }
}
```

`BaseIri` anchors the JSON-LD `@id` / `@context` IRIs and matches Public.Api's base.
The worker connects to `ieq` as the least-privilege `export_worker` role (SELECT on
measurements/parameter_ranges, SELECT+INSERT on measurement_exports).

## Running

```bash
dotnet run --project src/Ambiquality.Export.Worker   # needs the ieq db; dev uses FileSystem storage
```
