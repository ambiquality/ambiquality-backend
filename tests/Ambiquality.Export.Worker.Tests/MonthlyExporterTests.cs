using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Ambiquality.Core.Domain.Measurements;
using Ambiquality.Export.Worker;
using Ambiquality.Export.Worker.Exporting;
using Ambiquality.Export.Worker.Persistence;
using Ambiquality.Export.Worker.Serialization;
using Ambiquality.Export.Worker.Storage;
using Ambiquality.Export.Worker.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ambiquality.Export.Worker.Tests;

/// <summary>
/// Integration coverage for the export pipeline against a real TimescaleDB hypertable:
/// the month's measurements are streamed, compressed and written, and the export row is
/// recorded with the right key, format, and record count.
/// </summary>
public sealed class MonthlyExporterTests : IAsyncLifetime
{
    private readonly IeqPostgresFixture _postgres = new();
    private NpgsqlDataSource _dataSource = null!;
    private string _exportDir = null!;

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        _dataSource = NpgsqlDataSource.Create(_postgres.ConnectionString);
        _exportDir = Path.Combine(Path.GetTempPath(), $"exports-{Guid.NewGuid():N}");
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _postgres.DisposeAsync();
        if (Directory.Exists(_exportDir))
            Directory.Delete(_exportDir, recursive: true);
    }

    private MonthlyExporter NewExporter()
    {
        var options = Options.Create(new ExportOptions
        {
            StorageType = "FileSystem",
            BaseIri = "https://example.org",
            FileSystem = new FileSystemOptions { BasePath = _exportDir, PublicBaseUrl = "https://dl.example.org" }
        });
        var repository = new ExportRepository(_dataSource);
        var storage = new FileSystemExportStorage(options);
        return new MonthlyExporter(repository, storage, options, NullLogger<MonthlyExporter>.Instance);
    }

    private async Task SeedAsync(params (string parameter, double value, DateTime receivedAt)[] rows)
    {
        await using var db = _postgres.NewContext();
        foreach (var (parameter, value, receivedAt) in rows)
        {
            var m = Measurement.Record(
                sensorId: Guid.NewGuid(),
                parameterCode: parameter,
                value: value,
                unit: "ppm",
                observedAt: receivedAt.AddSeconds(-1),
                receivedAt: receivedAt);
            db.Measurements.Add(m);
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ExportAsync_Csv_WritesZippedArchiveAndRecordsMetadata()
    {
        await SeedAsync(
            ("co2", 800, new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc)),
            ("co2", 810, new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc)),
            // Outside the May window — must not appear in the export.
            ("co2", 999, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));

        var exporter = NewExporter();
        var csv = exporter.Formats.Single(f => f.MediaType == "text/csv");

        await exporter.ExportAsync(new ExportMonth(2026, 5), csv, CancellationToken.None);

        var zipPath = Path.Combine(_exportDir,
            "exports", "2026", "05", "measurements-2026-05.csv.zip");
        Assert.True(File.Exists(zipPath));

        using (var archive = ZipFile.OpenRead(zipPath))
        {
            var entry = archive.GetEntry("measurements.csv");
            Assert.NotNull(entry);
            using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
            var content = await reader.ReadToEndAsync();
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(CsvMeasurementSerializer.Header, lines[0]);
            Assert.Equal(2, lines.Length - 1); // header + 2 May rows, June excluded
        }

        await using var db = _postgres.NewContext();
        var export = db.MeasurementExports.Single();
        Assert.Equal((short)2026, export.Year);
        Assert.Equal((short)5, export.Month);
        Assert.Equal("text/csv", export.MediaType);
        Assert.Equal("application/zip", export.CompressFormat);
        Assert.Equal("exports/2026/05/measurements-2026-05.csv.zip", export.FileKey);
        Assert.Equal("https://dl.example.org/exports/2026/05/measurements-2026-05.csv.zip", export.DownloadUrl);
        Assert.Equal(2, export.RecordCount);
        Assert.True(export.FileSizeBytes > 0);
    }

    [Fact]
    public async Task ExportAsync_JsonLd_WritesGraphAndRecordsMetadata()
    {
        await SeedAsync(
            ("co2", 800, new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc)));

        var exporter = NewExporter();
        var jsonLd = exporter.Formats.Single(f => f.MediaType == "application/ld+json");

        await exporter.ExportAsync(new ExportMonth(2026, 5), jsonLd, CancellationToken.None);

        var zipPath = Path.Combine(_exportDir,
            "exports", "2026", "05", "measurements-2026-05.jsonld.zip");
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.GetEntry("measurements.jsonld");
        Assert.NotNull(entry);
        using var doc = await JsonDocument.ParseAsync(entry!.Open());
        Assert.Single(doc.RootElement.GetProperty("@graph").EnumerateArray());

        await using var db = _postgres.NewContext();
        var export = db.MeasurementExports.Single();
        Assert.Equal("application/ld+json", export.MediaType);
        Assert.Equal(1, export.RecordCount);
    }

    [Fact]
    public async Task GetExportedMediaTypes_ReflectsRecordedExports()
    {
        await SeedAsync(("co2", 800, new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc)));
        var exporter = NewExporter();
        var repository = new ExportRepository(_dataSource);

        await exporter.ExportAsync(new ExportMonth(2026, 5),
            exporter.Formats.Single(f => f.MediaType == "text/csv"), CancellationToken.None);

        var done = await repository.GetExportedMediaTypesAsync(2026, 5, CancellationToken.None);
        Assert.Contains("text/csv", done);
        Assert.DoesNotContain("application/ld+json", done);
    }
}
