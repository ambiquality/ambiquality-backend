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
                + "Negotiates application/json (default), application/ld+json, and text/csv.");

        group.MapMethods("/{id:guid}", ["GET", "HEAD"], GetObservationById)
            .WithName("GetObservationById")
            .WithSummary("Get an observation by id")
            .WithDescription("The stable IRI target for a single observation (JSON or JSON-LD).");
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
        var items = result.Items.Select(m => ObservationResponse.From(m, iri)).ToList();

        ResponseHeaders.SetListHeaders(http, iri, "observation");

        string? nextIri = result.NextCursor is null
            ? null
            : iri.ObservationsNext(result.NextCursor.Encode(), QueryWithoutCursor(http.Request));

        if (format == ResponseFormat.JsonLd)
            return Results.Json(ObservationJsonLd.ToGraph(items, iri, nextIri), contentType: Constants.MediaTypeJsonLd);

        return Results.Ok(new ObservationPage(items, result.NextCursor?.Encode(), nextIri, Constants.LicenseIri));
    }

    private static async Task<IResult> GetObservationById(
        Guid id,
        HttpContext http,
        IeqDbContext db,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format == ResponseFormat.Csv)
            return Problems.UnsupportedMediaType();

        var measurement = await ObservationQuery.GetByIdAsync(db, id, ct);
        if (measurement is null)
            return Problems.NotFound($"No observation with id '{id}'.");

        var iri = IriBuilder.ForRequest(http.Request, configuration);
        var observation = ObservationResponse.From(measurement, iri);

        ResponseHeaders.SetListHeaders(http, iri, "observation");

        if (format == ResponseFormat.JsonLd)
            return Results.Json(ObservationJsonLd.ToResource(observation, iri, includeContext: true),
                contentType: Constants.MediaTypeJsonLd);

        return Results.Ok(observation);
    }

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
