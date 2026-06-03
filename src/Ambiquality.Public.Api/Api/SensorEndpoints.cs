using Ambiquality.Public.Api.Infrastructure.Catalog;

namespace Ambiquality.Public.Api.Api;

/// <summary>Sensor catalog: a single sensor by id. GET+HEAD, JSON / JSON-LD (sosa:Sensor).</summary>
public static class SensorEndpoints
{
    public static void MapSensorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup($"/{Constants.ApiVersion}/sensors").WithTags("Sensors");

        group.MapMethods("/{id:guid}", ["GET", "HEAD"], GetSensorById)
            .WithName("GetSensorById")
            .WithSummary("Get a sensor by id");
    }

    private static async Task<IResult> GetSensorById(
        Guid id, HttpContext http, IEvidenceCatalog catalog, IConfiguration config, CancellationToken ct)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format == ResponseFormat.Csv)
            return Problems.UnsupportedMediaType();

        var row = await catalog.GetSensorAsync(id, ct);
        if (row is null)
            return Problems.NotFound($"No sensor with id '{id}'.");

        var iri = IriBuilder.ForRequest(http.Request, config);
        var sensor = CatalogMappers.ToResponse(row, iri);
        ResponseHeaders.SetListHeaders(http, iri, "sensor");

        return format == ResponseFormat.JsonLd
            ? Results.Json(CatalogJsonLd.ToSensor(sensor, iri), contentType: Constants.MediaTypeJsonLd)
            : Results.Ok(sensor);
    }
}
