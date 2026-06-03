using System.IO.Compression;
using Ambiquality.Core.Domain.Measurements;
using Ambiquality.Export.Worker.Persistence;
using Ambiquality.Export.Worker.Serialization;
using Ambiquality.Export.Worker.Storage;
using Microsoft.Extensions.Options;

namespace Ambiquality.Export.Worker.Exporting;

/// <summary>
/// Exports a single (month, format): streams the month's measurements through the
/// format serializer into a zip entry, uploads the archive, and records the export
/// metadata. The serialized payload is staged in a temp file (not memory) so the
/// archive can be re-read for the upload without buffering the whole month; the temp
/// file is deleted afterwards.
/// </summary>
public sealed class MonthlyExporter(
    ExportRepository repository,
    ISensorPlacementCatalog placements,
    IExportStorage storage,
    IOptions<ExportOptions> options,
    ILogger<MonthlyExporter> logger)
{
    private readonly ExportOptions _options = options.Value;

    private readonly JsonLdMeasurementSerializer _jsonLd = new(options.Value.BaseIri);

    public IReadOnlyList<ExportFormat> Formats =>
    [
        new ExportFormat("text/csv", "csv", "measurements.csv",
            (rows, dest, _, ct) => CsvMeasurementSerializer.WriteAsync(rows, dest, ct)),
        new ExportFormat("application/ld+json", "jsonld", "measurements.jsonld",
            (rows, dest, foi, ct) => _jsonLd.WriteAsync(rows, dest, foi, ct))
    ];

    public async Task ExportAsync(ExportMonth month, ExportFormat format, CancellationToken ct)
    {
        var key = BuildKey(month, format);
        var tempPath = Path.GetTempFileName();

        // The room each sensor occupied over time, to stamp every observation's feature of
        // interest. The placement set is the bounded device registry, so it loads once here
        // (not per row) and resolves in memory.
        var featureOfInterest = await placements.LoadResolverAsync(ct);

        try
        {
            long recordCount;
            await using (var temp = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                using (var archive = new ZipArchive(temp, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var entry = archive.CreateEntry(format.ZipEntryName, CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    var rows = repository.StreamMonthAsync(month.StartUtc, month.NextMonthStartUtc, ct);
                    recordCount = await format.Serialize(rows, entryStream, featureOfInterest, ct);
                }

                temp.Position = 0;
                var url = await storage.UploadAsync(key, temp, "application/zip", ct);

                await repository.InsertExportAsync(new MeasurementExport
                {
                    Id = Guid.NewGuid(),
                    Year = month.Year,
                    Month = month.Month,
                    MediaType = format.MediaType,
                    CompressFormat = "application/zip",
                    FileKey = key,
                    DownloadUrl = url.ToString(),
                    FileSizeBytes = temp.Length,
                    RecordCount = recordCount,
                    ExportedAt = DateTimeOffset.UtcNow
                }, ct);

                logger.LogInformation(
                    "Exported {Records} measurements for {Year}-{Month:D2} as {MediaType} ({Bytes} bytes) to {Key}.",
                    recordCount, month.Year, month.Month, format.MediaType, temp.Length, key);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static string BuildKey(ExportMonth month, ExportFormat format) =>
        $"exports/{month.Year:D4}/{month.Month:D2}/measurements-{month.Year:D4}-{month.Month:D2}.{format.KeySuffix}.zip";
}
