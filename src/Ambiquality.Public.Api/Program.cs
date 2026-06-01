using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Public.Api.Api;
using Ambiquality.Public.Api.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Persistence (read-only; Public.Api never migrates) ----------------------
// IeqDbContext is owned by Ingestion.Api (which holds the MigrationsAssembly).
// Here it is registered without a migrations assembly: reads only.
builder.Services.AddDbContext<IeqDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("IeqDb")));

// --- Evidence catalog reader (raw read-only Npgsql against the evidence schema,
// same pattern as Ingestion.Api's SensorCatalog). Singleton owning the pool.
builder.Services.AddSingleton<IEvidenceCatalog>(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("EvidenceDb")
        ?? throw new InvalidOperationException("ConnectionStrings:EvidenceDb is not configured.");
    return new EvidenceCatalog(NpgsqlDataSource.Create(connectionString));
});

// --- Export catalog reader (raw read-only Npgsql against ieq.measurement_exports
// over the public_api connection). Lists published monthly archives as DCAT downloads.
builder.Services.AddSingleton<IExportCatalog>(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("IeqDb")
        ?? throw new InvalidOperationException("ConnectionStrings:IeqDb is not configured.");
    return new ExportCatalog(NpgsqlDataSource.Create(connectionString));
});

// --- CORS (open data: any origin may read) -----------------------------------
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// --- OpenAPI / error handling ------------------------------------------------
// No Bearer security scheme — the public API is unauthenticated by design (OFN).
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

// --- Middleware --------------------------------------------------------------

// HEAD must mirror GET (same status + headers) with an empty body
// (RFC 9110 §9.3.2). Routes are registered with MapMethods(["GET","HEAD"]) so
// the GET handler also runs for HEAD and serializes a body; discard it here
// while leaving headers such as Content-Type intact.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsHead(context.Request.Method))
    {
        var originalBody = context.Response.Body;
        context.Response.Body = Stream.Null;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }
    else
    {
        await next(context);
    }
});

app.UseCors();

// Serves the static JSON Schema documents under /v1/schema/*.json that every
// response references via its `Link: …; rel="describedby"` header.
app.UseStaticFiles();

// --- OpenAPI spec + Scalar UI (exposed in ALL environments — the spec is a
// deliverable, F15). Scalar mounts at "/scalar/{documentName}" by default, which
// does not collide with the "/v1/..." API routes.
app.MapOpenApi();
app.MapScalarApiReference();

// --- Routing -----------------------------------------------------------------
app.MapObservationEndpoints();
app.MapCsvEndpoints();
app.MapContextEndpoints();
app.MapBuildingEndpoints();
app.MapRoomEndpoints();
app.MapSensorEndpoints();
app.MapDcatCatalogEndpoints();

app.Run();

/// <summary>Exposed so WebApplicationFactory-based integration tests can boot the API.</summary>
public partial class Program;
