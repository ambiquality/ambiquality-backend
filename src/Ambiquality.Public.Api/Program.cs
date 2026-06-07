using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Public.Api.Api;
using Ambiquality.Public.Api.Infrastructure.Catalog;
using Ambiquality.Public.Api.Infrastructure.Observations;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
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

// --- Analytical measurement reader (raw read-only Npgsql over the public_api ieq
// connection) for the map snapshot + observation aggregation: TimescaleDB time_bucket,
// percentile_cont and DISTINCT ON, which EF cannot translate. Same singleton-owns-the-pool
// pattern as the catalog readers.
builder.Services.AddSingleton<IMeasurementReader>(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("IeqDb")
        ?? throw new InvalidOperationException("ConnectionStrings:IeqDb is not configured.");
    return new MeasurementReader(NpgsqlDataSource.Create(connectionString));
});

// --- Distributed cache for the map snapshot (the only call the map makes on load /
// filter change). Redis-backed when ConnectionStrings:Redis is set (production, shared
// across replicas); otherwise an in-memory store (tests, local single-process runs). The
// read path degrades gracefully if the cache faults — see JsonDistributedCache.
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
    builder.Services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
else
    builder.Services.AddDistributedMemoryCache();

// --- CORS (open data: any origin may read) -----------------------------------
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// --- OpenAPI / error handling ------------------------------------------------
// Publishable spec (F15): document-level metadata so the generated OpenAPI is a
// self-describing open-data deliverable. No Bearer security scheme — the public
// API is unauthenticated by design (OFN). The license mirrors the CC BY 4.0 that
// every response body / Link header already advertises, and the publisher matches
// the DCAT catalog (CatalogEndpoints).
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Ambiquality Public API",
            Version = Constants.ApiVersion,
            Description =
                "Read-only open-data API for the Ambiquality IEQ (Indoor Environmental "
                + "Quality) monitoring platform: time-series observations (JSON, JSON-LD, CSV), "
                + "the building/room/sensor evidence catalog, and a DCAT-AP 3.0 dataset "
                + "description. All responses are published under CC BY 4.0.",
            Contact = new OpenApiContact { Name = "Vilém Charwot, VŠE Prague" },
            License = new OpenApiLicense
            {
                Name = "CC BY 4.0",
                Identifier = "CC-BY-4.0",
                Url = new Uri(Constants.LicenseIri)
            }
        };

        // Advertise the externally-reachable base URL so Scalar "Try it" and any
        // generated client target the real deployment. Behind Caddy the API is
        // served under /public with the prefix stripped (handle_path), so the
        // operator sets PublicApi:BaseIri to the external versioned root
        // (e.g. https://data.ambiquality.org/v1). The document paths already carry
        // the /v1 segment, so strip a trailing /v1 here to avoid doubling it.
        var baseIri = context.ApplicationServices
            .GetRequiredService<IConfiguration>()["PublicApi:BaseIri"];
        if (!string.IsNullOrWhiteSpace(baseIri))
        {
            var origin = baseIri.TrimEnd('/');
            if (origin.EndsWith($"/{Constants.ApiVersion}", StringComparison.Ordinal))
                origin = origin[..^$"/{Constants.ApiVersion}".Length];
            document.Servers = [new OpenApiServer { Url = origin }];
        }

        return Task.CompletedTask;
    });
});
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
app.MapObservationAggregateEndpoints();
app.MapMapEndpoints();
app.MapCsvEndpoints();
app.MapContextEndpoints();
app.MapPropertyEndpoints();
app.MapCodelistEndpoints();
app.MapBuildingEndpoints();
app.MapRoomEndpoints();
app.MapSensorEndpoints();
app.MapDcatCatalogEndpoints();

app.Run();

/// <summary>Exposed so WebApplicationFactory-based integration tests can boot the API.</summary>
public partial class Program;
