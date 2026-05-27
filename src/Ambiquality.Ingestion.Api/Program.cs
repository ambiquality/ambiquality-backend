using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Ingestion.Api.Api;
using Ambiquality.Ingestion.Api.Application;
using Ambiquality.Ingestion.Api.Application.Abstractions;
using Ambiquality.Ingestion.Api.Infrastructure;
using Ambiquality.Ingestion.Api.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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

// --- Application -------------------------------------------------------------
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IngestMeasurementHandler>();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapMeasurementEndpoints();

app.Run();

/// <summary>Exposed so WebApplicationFactory-based integration tests can boot the API.</summary>
public partial class Program;
