using Ambiquality.Evidence.Api.Api;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Application.Rooms;
using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Ambiquality.Evidence.Api.Infrastructure;
using Ambiquality.Evidence.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Persistence ---------------------------------------------------------------
builder.Services.AddDbContext<EvidenceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EvidenceDb"),
        o => o.MigrationsHistoryTable("__EFMigrationsHistory", "evidence")
             .MigrationsAssembly(typeof(Program).Assembly.GetName().Name)));

builder.Services.AddScoped<IBuildingRepository, BuildingRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();

// --- Infrastructure -----------------------------------------------------------
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICurrentUser, CurrentUserStub>();

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

// --- OpenAPI / Swagger -------------------------------------------------------
builder.Services.AddOpenApi();
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// --- Routing -------------------------------------------------------
app.MapBuildingEndpoints();
app.MapRoomEndpoints();

app.Run();

/// <summary>Exposed so WebApplicationFactory-based integration tests can boot the API.</summary>
public partial class Program;

/// <summary>
/// Stub implementation of ICurrentUser. In production, this should extract
/// user information from JWT claims via authentication middleware.
/// </summary>
public sealed class CurrentUserStub : ICurrentUser
{
    public Guid AuthUserId => Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid ProjectionId => Guid.Parse("00000000-0000-0000-0000-000000000001");
}
