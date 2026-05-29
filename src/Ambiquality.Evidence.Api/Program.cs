using System.Text;
using Ambiquality.Evidence.Api.Api;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Application.Rooms;
using Ambiquality.Evidence.Api.Application.Sensors;
using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Ambiquality.Evidence.Api.Domain.Sensors;
using Ambiquality.Evidence.Api.Infrastructure;
using Ambiquality.Evidence.Api.Infrastructure.Persistence;
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
app.MapBuildingEndpoints();
app.MapRoomEndpoints();
app.MapSensorEndpoints();

app.Run();

/// <summary>Exposed so WebApplicationFactory-based integration tests can boot the API.</summary>
public partial class Program;
