namespace Ambiquality.Core.Domain.Measurements;

/// <summary>
/// Metadata for one published monthly export object in object storage. The
/// Export.Worker inserts one row per (year, month, media type) after a successful
/// upload; Public.Api reads them to list <c>dcat:Distribution</c> download entries
/// in the DCAT-AP catalog. The export objects themselves are immutable snapshots of
/// a fully elapsed calendar month.
/// </summary>
public sealed class MeasurementExport
{
    public Guid Id { get; set; }
    public short Year { get; set; }
    public short Month { get; set; }

    /// <summary>"text/csv" | "application/ld+json".</summary>
    public string MediaType { get; set; } = null!;

    public string CompressFormat { get; set; } = "application/zip";
    public string FileKey { get; set; } = null!;
    public string DownloadUrl { get; set; } = null!;
    public long? FileSizeBytes { get; set; }
    public long? RecordCount { get; set; }
    public DateTimeOffset ExportedAt { get; set; }
}
