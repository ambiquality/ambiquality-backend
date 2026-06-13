using System.Text;
using Ambiquality.Core.Domain.Vocabulary;
using Ambiquality.Evidence.Api.Api;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Application.Rooms;
using Ambiquality.Evidence.Api.Application.Sensors;
using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Ambiquality.Evidence.Api.Domain.Sensors;
using Ambiquality.Evidence.Api.Infrastructure;
using Ambiquality.Evidence.Api.Infrastructure.Persistence;
using Ambiquality.Evidence.Api.Infrastructure.Ruian;
using Ambiquality.Evidence.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration / options -------------------------------------------------
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddSingleton(jwtOptions);

// POD-04: operator-extensible codelists and quantities — apply the extensions file
// (if configured) before anything validates against the vocabularies, and register
// each extension property so sensors can declare it.
var vocabularyExtensions = VocabularyExtensionsLoader.LoadAndApply(
    builder.Configuration[VocabularyExtensionsLoader.PathConfigKey]);
foreach (var property in vocabularyExtensions?.Properties ?? [])
    MeasuredParameter.Register(property.Code);

// --- Persistence ---------------------------------------------------------------
builder.Services.AddDbContext<EvidenceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EvidenceDb"),
        o => o.MigrationsHistoryTable("__EFMigrationsHistory", "evidence")
             .MigrationsAssembly(typeof(Program).Assembly.GetName().Name)));

builder.Services.AddScoped<IBuildingRepository, BuildingRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<ISensorRepository, SensorRepository>();
builder.Services.AddScoped<IUserProjectionRepository, UserProjectionRepository>();

// --- Infrastructure -----------------------------------------------------------
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ISlugGenerator, RandomSlugGenerator>();
builder.Services.AddSingleton<ISensorApiKeyService, SensorApiKeyService>();

// RÚIAN address autocomplete (ČÚZK ArcGIS, open data / CC BY 4.0). Base URL is configurable so
// it can be pointed at a mirror; the 5 s timeout keeps a slow upstream from hanging the form.
builder.Services.AddHttpClient<IAddressGeocoder, RuianGeocoderClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Ruian:BaseUrl"]
        ?? "https://ags.cuzk.gov.cz/arcgis/rest/services/RUIAN/MapServer/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());

// --- AuthN / AuthZ -----------------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the raw "sub" claim instead of remapping it to the long
        // ClaimTypes.NameIdentifier URI, so the middleware can read it directly.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// --- Application handlers -------------------------------------------------------
builder.Services.AddScoped<RegisterBuildingHandler>();
builder.Services.AddScoped<ChangeBuildingNameHandler>();
builder.Services.AddScoped<ChangeBuildingAddressHandler>();
builder.Services.AddScoped<ChangeBuildingTypeHandler>();
builder.Services.AddScoped<ChangeBuildingLocationHandler>();
builder.Services.AddScoped<ChangeBuildingYearsHandler>();

builder.Services.AddScoped<RegisterRoomHandler>();
builder.Services.AddScoped<ChangeRoomNameHandler>();
builder.Services.AddScoped<ChangeRoomFloorHandler>();
builder.Services.AddScoped<ChangeRoomFunctionHandler>();
builder.Services.AddScoped<ChangeRoomExposureHandler>();
builder.Services.AddScoped<ChangeRoomGeometryHandler>();
builder.Services.AddScoped<ChangeRoomVentilationHandler>();
builder.Services.AddScoped<AddRoomPollutionSourceHandler>();
builder.Services.AddScoped<RemoveRoomPollutionSourceHandler>();

builder.Services.AddScoped<RegisterSensorHandler>();
builder.Services.AddScoped<ChangeSensorIdentityHandler>();
builder.Services.AddScoped<ChangeSensorPlacementHandler>();
builder.Services.AddScoped<ChangeSensorStatusHandler>();
builder.Services.AddScoped<AddSensorMeasuredParameterHandler>();
builder.Services.AddScoped<RemoveSensorMeasuredParameterHandler>();
builder.Services.AddScoped<ChangeSensorInstallationHandler>();

// --- OpenAPI / Swagger -------------------------------------------------------
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        var components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT access token issued by the Ambiquality Auth API."
        };
        return Task.CompletedTask;
    });

    // Advertise the Bearer requirement only on endpoints that require authorization.
    options.AddOperationTransformer((operation, context, ct) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer")] = []
            });
        }
        return Task.CompletedTask;
    });
});
builder.Services.AddProblemDetails();

// --- CORS --------------------------------------------------------------------
// The operator SPA is served from a different origin than this API, so browsers
// require CORS. Bearer-token flow, no cookies, so no AllowCredentials. Allowed
// origins come from config (comma-separated) so dev (localhost) and prod (the
// frontend domain) are both expressible; empty means no CORS headers.
var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins.Length > 0)
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
    }));

var app = builder.Build();

// --- Middleware -------------------------------------------------------

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
app.UseAuthentication();
app.UseAuthorization();

// Resolve the current user's projection once the JWT is validated, so handlers
// can read ICurrentUser. Must run after authentication and before the endpoints.
app.UseMiddleware<CurrentUserMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// --- Routing -------------------------------------------------------
// Every endpoint is mounted under /v1 so the write API is versioned like the
// public open-data contract (Caddy strips the /evidence prefix, leaving /v1/...).
var v1 = app.MapGroup($"/{Constants.ApiVersion}");
v1.MapBuildingEndpoints();
v1.MapRoomEndpoints();
v1.MapSensorEndpoints();
v1.MapAddressLookupEndpoints();

app.Run();

/// <summary>Exposed so WebApplicationFactory-based integration tests can boot the API.</summary>
public partial class Program;
