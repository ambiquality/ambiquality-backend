using System.IO.Compression;
using Ambiquality.Core.Domain.Measurements;
using Ambiquality.Export.Worker.Persistence;
using Ambiquality.Export.Worker.Serialization;
using Ambiquality.Export.Worker.Storage;
using Microsoft.Extensions.Options;

namespace Ambiquality.Export.Worker.Exporting;

/// <summary>
/// Exports a single (month, format): streams the month's measurements through the
/// format serializer into a gzip-compressed temp file, uploads it, and records the
/// export metadata. Single-file gzip (not a zip archive) keeps the download simple —
/// no container to unwrap, just decompress and read. The serialized payload is staged
/// in a temp file so the compressed bytes can be re-read for upload without buffering
/// the whole month in memory; the temp file is deleted afterwards.
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
        new ExportFormat("text/csv", "csv",
            (rows, dest, _, ct) => CsvMeasurementSerializer.WriteAsync(rows, dest, ct)),
        new ExportFormat("application/ld+json", "jsonld",
            (rows, dest, foi, ct) => _jsonLd.WriteAsync(rows, dest, foi, ct))
    ];

    public async Task ExportAsync(ExportMonth month, ExportFormat format, CancellationToken ct)
    {
        var key = BuildKey(month, format);
        var tempPath = Path.GetTempFileName();

        var featureOfInterest = await placements.LoadResolverAsync(ct);

        try
        {
            long recordCount;
            await using (var temp = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                await using (var gzip = new GZipStream(temp, CompressionLevel.Optimal, leaveOpen: true))
                {
                    var rows = repository.StreamMonthAsync(month.StartUtc, month.NextMonthStartUtc, ct);
                    recordCount = await format.Serialize(rows, gzip, featureOfInterest, ct);
                }

                temp.Position = 0;
                var url = await storage.UploadAsync(key, temp, "application/gzip", ct);

                await repository.InsertExportAsync(new MeasurementExport
                {
                    Id = Guid.NewGuid(),
                    Year = month.Year,
                    Month = month.Month,
                    MediaType = format.MediaType,
                    CompressFormat = "application/gzip",
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
        $"exports/{month.Year:D4}/{month.Month:D2}/measurements-{month.Year:D4}-{month.Month:D2}.{format.KeySuffix}.gz";
}
