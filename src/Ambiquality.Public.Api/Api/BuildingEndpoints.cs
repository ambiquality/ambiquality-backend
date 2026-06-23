using Ambiquality.Public.Api.Infrastructure.Catalog;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// Building catalog: list (filterable by type and bounding box), single building,
/// and the building's rooms. All routes are GET+HEAD and negotiate JSON / JSON-LD.
/// Addresses follow the Czech OFN "Adresy" standard; coordinates are the precise
/// stored values (open data; no anonymization).
/// </summary>
public static class BuildingEndpoints
{
    public static void MapBuildingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup($"/{Constants.ApiVersion}/buildings").WithTags("Buildings");

        group.MapMethods("/", ["GET", "HEAD"], ListBuildings)
            .WithName("ListBuildings")
            .WithSummary("List buildings")
            .WithDescription(
                "Filters: buildingType, bbox (minLon,minLat,maxLon,maxLat), page, pageSize. "
                + "Offset/page paged with a total count: the catalog is small and bounded, so a page "
                + "index is friendlier than the keyset cursor used by the unbounded observations feed. "
                + "Each building carries a Czech OFN address (see the Address schema). Negotiable as "
                + "JSON (default) or JSON-LD (Accept: application/ld+json).")
            .Produces<CatalogPage<BuildingResponse>>(StatusCodes.Status200OK,
                contentType: Constants.MediaTypeJson, Constants.MediaTypeJsonLd)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);

        group.MapMethods("/{id:guid}", ["GET", "HEAD"], GetBuildingById)
            .WithName("GetBuildingById")
            .WithSummary("Get a building by id")
            .WithDescription(
                "Returns one building with its current OFN address (see the Address schema) and "
                + "precise coordinates. Negotiable as JSON (default) or JSON-LD; the JSON-LD form "
                + "emits a conformant OFN Adresa node with dereferenceable RÚIAN IRIs.")
            .Produces<BuildingResponse>(StatusCodes.Status200OK,
                contentType: Constants.MediaTypeJson, Constants.MediaTypeJsonLd)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);

        group.MapMethods("/{buildingId:guid}/rooms", ["GET", "HEAD"], ListBuildingRooms)
            .WithName("ListBuildingRooms")
            .WithSummary("List rooms of a building")
            .WithDescription("Filters: roomFunction, minExposure (minutes), page, pageSize.")
            .Produces<CatalogPage<RoomResponse>>(StatusCodes.Status200OK,
                contentType: Constants.MediaTypeJson, Constants.MediaTypeJsonLd)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);
    }

    private static async Task<IResult> ListBuildings(
        HttpContext http, IEvidenceCatalog catalog, IConfiguration config, CancellationToken ct)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format == ResponseFormat.Csv)
            return Problems.UnsupportedMediaType();

        var buildingType = http.Request.Query["buildingType"].FirstOrDefault();
        BoundingBox? bbox = null;
        var bboxRaw = http.Request.Query["bbox"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(bboxRaw))
        {
            if (!BoundingBox.TryParse(bboxRaw, out var parsed))
                return Problems.InvalidBbox();
            bbox = parsed;
        }

        var (page, pageSize) = CatalogPaging.Parse(http.Request);
        var iri = IriBuilder.ForRequest(http.Request, config);
        var (rows, total) = await catalog.GetBuildingsAsync(buildingType, bbox, page, pageSize, ct);
        var items = rows.Select(r => CatalogMappers.ToResponse(r, iri)).ToList();

        ResponseHeaders.SetListHeaders(http, iri, "building");
        var next = CatalogPaging.NextLink(iri.Buildings(), page, pageSize, total,
            CatalogPaging.QueryExcept(http.Request, "page", "pageSize"));

        if (format == ResponseFormat.JsonLd)
            return Results.Json(CatalogJsonLd.ToGraph(items.Select(b => CatalogJsonLd.ToBuilding(b, iri))),
                contentType: Constants.ContentTypeJsonLd);

        return Results.Ok(new CatalogPage<BuildingResponse>(items, page, pageSize, total, next, Constants.LicenseIri));
    }

    private static async Task<IResult> GetBuildingById(
        Guid id, HttpContext http, IEvidenceCatalog catalog, IConfiguration config, CancellationToken ct)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format == ResponseFormat.Csv)
            return Problems.UnsupportedMediaType();

        var row = await catalog.GetBuildingAsync(id, ct);
        if (row is null)
            return Problems.NotFound($"No building with id '{id}'.");

        var iri = IriBuilder.ForRequest(http.Request, config);
        var building = CatalogMappers.ToResponse(row, iri);
        ResponseHeaders.SetListHeaders(http, iri, "building");

        return format == ResponseFormat.JsonLd
            ? Results.Json(CatalogJsonLd.ToBuilding(building, iri), contentType: Constants.ContentTypeJsonLd)
            : Results.Ok(building);
    }

    private static async Task<IResult> ListBuildingRooms(
        Guid buildingId, HttpContext http, IEvidenceCatalog catalog, IConfiguration config, CancellationToken ct)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format == ResponseFormat.Csv)
            return Problems.UnsupportedMediaType();

        var roomFunction = http.Request.Query["roomFunction"].FirstOrDefault();
        int? minExposure = int.TryParse(http.Request.Query["minExposure"], out var m) ? m : null;

        var (page, pageSize) = CatalogPaging.Parse(http.Request);
        var iri = IriBuilder.ForRequest(http.Request, config);
        var (rows, total) = await catalog.GetRoomsAsync(buildingId, roomFunction, minExposure, page, pageSize, ct);
        var items = rows.Select(r => CatalogMappers.ToResponse(r, iri)).ToList();

        ResponseHeaders.SetListHeaders(http, iri, "room");
        var next = CatalogPaging.NextLink(iri.BuildingRooms(buildingId), page, pageSize, total,
            CatalogPaging.QueryExcept(http.Request, "page", "pageSize"));

        if (format == ResponseFormat.JsonLd)
            return Results.Json(CatalogJsonLd.ToGraph(items.Select(r => CatalogJsonLd.ToRoom(r, iri))),
                contentType: Constants.ContentTypeJsonLd);

        return Results.Ok(new CatalogPage<RoomResponse>(items, page, pageSize, total, next, Constants.LicenseIri));
    }
}
