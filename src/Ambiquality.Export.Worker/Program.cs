using Ambiquality.Export.Worker;
using Ambiquality.Export.Worker.Exporting;
using Ambiquality.Export.Worker.Persistence;
using Ambiquality.Export.Worker.Storage;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ExportOptions>(builder.Configuration.GetSection(ExportOptions.SectionName));

// ieq: read measurements, read/write the export catalog. No EF Core — raw Npgsql,
// same pattern as Ingestion.Worker.
builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("IeqDb")
        ?? throw new InvalidOperationException("Missing 'IeqDb' connection string.");
    return NpgsqlDataSource.Create(connectionString);
});

builder.Services.AddSingleton<ExportRepository>();

// evidence: read-only, to resolve each observation's feature of interest (the room the
// sensor occupied at observation time) from the sensor placement history.
builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("EvidenceDb")
        ?? throw new InvalidOperationException("Missing 'EvidenceDb' connection string.");
    return new EvidenceDataSource(NpgsqlDataSource.Create(connectionString));
});

builder.Services.AddSingleton<ISensorPlacementCatalog, SensorPlacementCatalog>();

var storageType = builder.Configuration.GetSection(ExportOptions.SectionName)["StorageType"];
if (string.Equals(storageType, "S3", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IExportStorage, S3ExportStorage>();
else
    builder.Services.AddSingleton<IExportStorage, FileSystemExportStorage>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MonthlyExporter>();
builder.Services.AddHostedService<MonthlyExportService>();

var host = builder.Build();
host.Run();
