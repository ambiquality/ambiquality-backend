using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Public.Api.Application.Observations;
using Ambiquality.Public.Api.Infrastructure.Catalog;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// CSV export of observations (F17). The same filters as the JSON list apply, but
/// there is no page-size cap — the full filtered set streams with bounded memory.
/// </summary>
public static class CsvEndpoints
{
    public static void MapCsvEndpoints(this WebApplication app)
    {
        app.MapMethods($"/{Constants.ApiVersion}/observations.csv", ["GET", "HEAD"],
            (HttpContext http, IeqDbContext db, IEvidenceCatalog catalog, CancellationToken ct)
                => StreamObservations(http.Request, db, catalog, ct))
            .WithTags("Observations")
            .WithName("ExportObservationsCsv")
            .WithSummary("Export observations as CSV")
            .WithDescription("Streams the filtered observation set as a downloadable CSV archive (CC BY 4.0).");
    }

    /// <summary>
    /// Parses the observation filters and returns the CSV streaming result. Shared
    /// by the dedicated <c>.csv</c> route and the list endpoint's <c>text/csv</c> path.
    /// </summary>
    public static async Task<IResult> StreamObservations(
        HttpRequest request, IeqDbContext db, IEvidenceCatalog catalog, CancellationToken ct)
    {
        if (ObservationRequestParser.TryParse(request, out var filter) is { } problem)
            return problem;

        var rows = await ObservationQuery.StreamAsync(db, catalog, filter, ct);
        return new ObservationCsvStreamer(rows);
    }
}
