using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Public.Api.Application.Observations;
using Ambiquality.Public.Api.Infrastructure.Catalog;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// SSN/SOSA observation endpoints: a keyset-paginated list and a stable per-id
/// resource, both content-negotiable as plain JSON or JSON-LD. A <c>text/csv</c>
/// Accept on the list delegates to the CSV export.
/// </summary>
public static class ObservationEndpoints
{
    public static void MapObservationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup($"/{Constants.ApiVersion}/observations").WithTags("Observations");

        group.MapMethods("/", ["GET", "HEAD"], ListObservations)
            .WithName("ListObservations")
            .WithSummary("List observations")
            .WithDescription(
                "Keyset-paginated measurements as SSN/SOSA observations. Filters: from, to, "
                + "sensorId, parameterCode, buildingId, roomId, bbox, includeInvalid, limit, cursor. "
                + "Negotiates application/json (default), application/ld+json, and text/csv. "
                + "Paging is keyset (opaque cursor), not offset: the observations hypertable is "
                + "append-only and unbounded, so a cursor gives stable, gap-free pages and constant "
                + "latency at any depth — unlike the catalog endpoints, which use small, bounded "
                + "offset/page collections.")
            .Produces<ObservationPage>(StatusCodes.Status200OK,
                contentType: Constants.MediaTypeJson,
                Constants.MediaTypeJsonLd, Constants.MediaTypeCsv)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);

        group.MapMethods("/{id:guid}", ["GET", "HEAD"], GetObservationById)
            .WithName("GetObservationById")
            .WithSummary("Get an observation by id")
            .WithDescription("The stable IRI target for a single observation (JSON or JSON-LD).")
            .Produces<ObservationResponse>(StatusCodes.Status200OK,
                contentType: Constants.MediaTypeJson,
                Constants.MediaTypeJsonLd)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);
    }

    private static async Task<IResult> ListObservations(
        HttpContext http,
        IeqDbContext db,
        IEvidenceCatalog catalog,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format))
            return Problems.UnsupportedMediaType();

        if (format == ResponseFormat.Csv)
            return await CsvEndpoints.StreamObservations(http.Request, db, catalog, configuration, ct);

        if (ObservationRequestParser.TryParse(http.Request, out var filter) is { } problem)
            return problem;

        var iri = IriBuilder.ForRequest(http.Request, configuration);
        var result = await ObservationQuery.PageAsync(db, catalog, filter, ct);

        // Resolve each observation's feature of interest (the room the sensor was in at
        // observation time) from one batched placement lookup over the page's sensors.
        var sensorIds = result.Items.Select(m => m.SensorId).Distinct().ToList();
        var foi = new FeatureOfInterestResolver(await catalog.GetSensorPlacementsAsync(sensorIds, ct));
        var items = result.Items
            .Select(m => ObservationResponse.From(m, iri, FeatureOfInterestIri(m, foi, iri)))
            .ToList();

        ResponseHeaders.SetListHeaders(http, iri, "observation");

        string? nextIri = result.NextCursor is null
            ? null
            : iri.ObservationsNext(result.NextCursor.Encode(), QueryWithoutCursor(http.Request));

        if (format == ResponseFormat.JsonLd)
            return Results.Json(ObservationJsonLd.ToGraph(items, iri, nextIri), contentType: Constants.ContentTypeJsonLd);

        return Results.Ok(new ObservationPage(items, result.NextCursor?.Encode(), nextIri, Constants.LicenseIri));
    }

    private static async Task<IResult> GetObservationById(
        Guid id,
        HttpContext http,
        IeqDbContext db,
        IEvidenceCatalog catalog,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format == ResponseFormat.Csv)
            return Problems.UnsupportedMediaType();

        var measurement = await ObservationQuery.GetByIdAsync(db, id, ct);
        if (measurement is null)
            return Problems.NotFound($"No observation with id '{id}'.");

        var iri = IriBuilder.ForRequest(http.Request, configuration);
        var foi = new FeatureOfInterestResolver(
            await catalog.GetSensorPlacementsAsync([measurement.SensorId], ct));
        var observation = ObservationResponse.From(measurement, iri, FeatureOfInterestIri(measurement, foi, iri));

        ResponseHeaders.SetListHeaders(http, iri, "observation");

        if (format == ResponseFormat.JsonLd)
            return Results.Json(ObservationJsonLd.ToResource(observation, iri, includeContext: true),
                contentType: Constants.ContentTypeJsonLd);

        return Results.Ok(observation);
    }

    /// <summary>The room IRI a measurement's sensor occupied at observation time, or null.</summary>
    private static string? FeatureOfInterestIri(
        Core.Domain.Measurements.Measurement m, FeatureOfInterestResolver foi, IriBuilder iri) =>
        foi.ResolveRoomId(m.SensorId, m.ObservedAt) is { } roomId ? iri.Room(roomId) : null;

    /// <summary>Re-renders the current query string without the <c>cursor</c> parameter.</summary>
    private static string QueryWithoutCursor(HttpRequest request)
    {
        var pairs = request.Query
            .Where(kv => !string.Equals(kv.Key, "cursor", StringComparison.OrdinalIgnoreCase))
            .SelectMany(kv => kv.Value.Select(v =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(v ?? string.Empty)}"));
        return string.Join('&', pairs);
    }
}
