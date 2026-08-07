using Ambiquality.Core.Domain.Vocabulary;
using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Observability;
using Ambiquality.Public.Api.Api;
using Ambiquality.Public.Api.Infrastructure.Catalog;
using Ambiquality.Public.Api.Infrastructure.Observations;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Npgsql;
using OpenTelemetry.Metrics;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Observability -----------------------------------------------------------
// OpenTelemetry metrics (RED + runtime + web-vitals RUM) exposed on a dedicated
// internal port (Observability:MetricsPort) via an HttpListener — never the Kestrel
// port behind Caddy, so /metrics cannot leak through the public ingress. Off in tests.
var observabilityEnabled = ObservabilityExtensions.IsEnabled(builder.Configuration);
var observabilityMetricsPort = ObservabilityExtensions.ResolveMetricsPort(builder.Configuration, 9467);
if (observabilityEnabled)
    builder.Services.AddAmbiqualityMetrics(observabilityMetricsPort,
        metrics => metrics.AddAspNetCoreInstrumentation());

// POD-04: operator-extensible codelists and quantities — applied before any
// endpoint publishes the vocabularies (codelist/property endpoints, SKOS labels).
VocabularyExtensionsLoader.LoadAndApply(
    builder.Configuration[VocabularyExtensionsLoader.PathConfigKey]);

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

    // Document the building address model (OFN Adresy / RÚIAN) field-by-field so the
    // generated spec and Scalar UI are self-explanatory — see AddressSchemaDocumentation.
    AddressSchemaDocumentation.Configure(options);
});
builder.Services.AddProblemDetails();

// --- Request logging ---------------------------------------------------------
// One structured line per request (method, path, status, duration) so the public
// read traffic and any slow queries are visible in the JSON console logs and the
// p95/p99 latency NFR can be observed. Bodies/headers are not logged (no auth, but
// keeps the line small under high read volume).
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
    logging.CombineLogs = true;
});

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

// Log every request (first so the duration covers the whole pipeline and the
// final status is recorded). Unhandled exceptions are still logged by the
// framework; no UseExceptionHandler so edge binding errors keep their native
// 400 (a blanket handler would rewrite BadHttpRequestException to 500).
app.UseHttpLogging();

app.UseCors();

// Rewrites a directory request to its default document (index.html) so the data-model
// spec at `/docs/` serves `wwwroot/docs/index.html` instead of 404-ing. Must run before
// UseStaticFiles. Safe at root: wwwroot has no top-level index.html, so it only matches
// `/docs/` and leaves the `/v1/...` API routes untouched.
app.UseDefaultFiles();

// Serves the static JSON Schema documents under /v1/schema/*.json that every
// response references via its `Link: …; rel="describedby"` header.
app.UseStaticFiles();

// --- OpenAPI spec + Scalar UI (exposed in ALL environments — the spec is a
// deliverable, F15). Scalar mounts at "/scalar/{documentName}" by default, which
// does not collide with the "/v1/..." API routes.
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "Ambiquality Public API";
    options.Theme = ScalarTheme.Purple;
    // Default HTTP client for "Try it" — no auth needed (all routes are public).
    options.DefaultHttpClient = new(ScalarTarget.Http, ScalarClient.HttpClient);
    // Each endpoint declares its own Produces content types, which Scalar uses to
    // populate the Accept header dropdown in Try it. No global override needed.
});

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
app.MapRumVitalsEndpoint();

app.Run();

/// <summary>Exposed so WebApplicationFactory-based integration tests can boot the API.</summary>
public partial class Program;
