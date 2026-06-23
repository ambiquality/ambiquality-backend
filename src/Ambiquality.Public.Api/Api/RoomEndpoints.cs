using Ambiquality.Public.Api.Infrastructure.Catalog;

namespace Ambiquality.Public.Api.Api;

/// <summary>Room catalog: a single room and the room's sensors. GET+HEAD, JSON / JSON-LD.</summary>
public static class RoomEndpoints
{
    public static void MapRoomEndpoints(this WebApplication app)
    {
        var group = app.MapGroup($"/{Constants.ApiVersion}/rooms").WithTags("Rooms");

        group.MapMethods("/{id:guid}", ["GET", "HEAD"], GetRoomById)
            .WithName("GetRoomById")
            .WithSummary("Get a room by id")
            .Produces<RoomResponse>(StatusCodes.Status200OK,
                contentType: Constants.MediaTypeJson, Constants.MediaTypeJsonLd)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);

        group.MapMethods("/{roomId:guid}/sensors", ["GET", "HEAD"], ListRoomSensors)
            .WithName("ListRoomSensors")
            .WithSummary("List sensors in a room")
            .WithDescription("Filters: parameterCode, status, page, pageSize.")
            .Produces<CatalogPage<SensorResponse>>(StatusCodes.Status200OK,
                contentType: Constants.MediaTypeJson, Constants.MediaTypeJsonLd)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);
    }

    private static async Task<IResult> GetRoomById(
        Guid id, HttpContext http, IEvidenceCatalog catalog, IConfiguration config, CancellationToken ct)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format == ResponseFormat.Csv)
            return Problems.UnsupportedMediaType();

        var row = await catalog.GetRoomAsync(id, ct);
        if (row is null)
            return Problems.NotFound($"No room with id '{id}'.");

        var iri = IriBuilder.ForRequest(http.Request, config);
        var room = CatalogMappers.ToResponse(row, iri);
        ResponseHeaders.SetListHeaders(http, iri, "room");

        return format == ResponseFormat.JsonLd
            ? Results.Json(CatalogJsonLd.ToRoom(room, iri), contentType: Constants.ContentTypeJsonLd)
            : Results.Ok(room);
    }

    private static async Task<IResult> ListRoomSensors(
        Guid roomId, HttpContext http, IEvidenceCatalog catalog, IConfiguration config, CancellationToken ct)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format == ResponseFormat.Csv)
            return Problems.UnsupportedMediaType();

        var parameterCode = http.Request.Query["parameterCode"].FirstOrDefault();
        var status = http.Request.Query["status"].FirstOrDefault();

        var (page, pageSize) = CatalogPaging.Parse(http.Request);
        var iri = IriBuilder.ForRequest(http.Request, config);
        var (rows, total) = await catalog.GetSensorsAsync(roomId, parameterCode, status, page, pageSize, ct);
        var items = rows.Select(r => CatalogMappers.ToResponse(r, iri)).ToList();

        ResponseHeaders.SetListHeaders(http, iri, "sensor");
        var next = CatalogPaging.NextLink(iri.RoomSensors(roomId), page, pageSize, total,
            CatalogPaging.QueryExcept(http.Request, "page", "pageSize"));

        if (format == ResponseFormat.JsonLd)
            return Results.Json(CatalogJsonLd.ToGraph(items.Select(s => CatalogJsonLd.ToSensor(s, iri))),
                contentType: Constants.ContentTypeJsonLd);

        return Results.Ok(new CatalogPage<SensorResponse>(items, page, pageSize, total, next, Constants.LicenseIri));
    }
}
