using Ambiquality.Core.Domain.Vocabulary;
using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Api.Api;
using Ambiquality.Ingestion.Api.Application;
using Ambiquality.Ingestion.Api.Application.Abstractions;
using Ambiquality.Ingestion.Api.Infrastructure;
using Ambiquality.Ingestion.Api.Infrastructure.Catalog;
using Ambiquality.Ingestion.Api.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// POD-04: operator-extensible quantities — extension parameters become resolvable
// (QUDT URIs) before any validation runs; their ranges are seeded into
// ieq.parameter_ranges after the app is built (below).
var vocabularyExtensions = VocabularyExtensionsLoader.LoadAndApply(
    builder.Configuration[VocabularyExtensionsLoader.PathConfigKey]);

// --- Persistence: ieq (read-write, owns migrations) --------------------------
builder.Services.AddDbContext<IeqDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("IeqDb"),
        o => o.MigrationsHistoryTable("__EFMigrationsHistory", "ieq")
             .MigrationsAssembly(typeof(Program).Assembly.GetName().Name)));

// --- Evidence catalog: read-only validation source ---------------------------
// Resolved lazily so design-time tooling (migrations) doesn't need the string.
// The ingestion_api role has SELECT on the evidence schema; reads are direct
// (not HTTP) to keep the hot path cheap.
builder.Services.AddSingleton<ISensorCatalog>(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("EvidenceDb")
        ?? throw new InvalidOperationException("Missing 'EvidenceDb' connection string.");
    return new SensorCatalog(NpgsqlDataSource.Create(connectionString));
});

// --- Ingestion queue: durable write-ahead log (Redis stream) -----------------
// The connection is resolved lazily so design-time tooling and tests that
// override the publisher never open a socket to Redis.
builder.Services.Configure<MeasurementQueueOptions>(
    builder.Configuration.GetSection(MeasurementQueueOptions.SectionName));
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Missing 'Redis' connection string.");
    return ConnectionMultiplexer.Connect(connectionString);
});
builder.Services.AddSingleton<IMeasurementQueuePublisher, RedisMeasurementQueuePublisher>();

// --- Application -------------------------------------------------------------
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IngestMeasurementHandler>();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Seed a permitted-value range per extension property (additive, idempotent —
// ON CONFLICT DO NOTHING so built-in and previously-seeded rows are never altered;
// concurrent replicas booting at once are safe).
if (vocabularyExtensions?.Properties is { Count: > 0 } extensionProperties)
{
    using var scope = app.Services.CreateScope();
    var ieq = scope.ServiceProvider.GetRequiredService<IeqDbContext>();
    foreach (var property in extensionProperties)
        await ieq.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO ieq.parameter_ranges (parameter_code, min_value, max_value, unit)
             VALUES ({property.Code}, {property.MinValue}, {property.MaxValue}, {property.Unit})
             ON CONFLICT (parameter_code) DO NOTHING
             """);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Mount under /v1 so the ingestion contract is versioned like the other services
// (Caddy strips the /ingestion prefix, leaving /v1/measurements).
app.MapGroup("/v1").MapMeasurementEndpoints();

app.Run();

/// <summary>Exposed so WebApplicationFactory-based integration tests can boot the API.</summary>
public partial class Program;
