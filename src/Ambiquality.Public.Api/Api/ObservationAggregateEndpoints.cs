using Ambiquality.Public.Api.Application.Observations;
using Ambiquality.Public.Api.Infrastructure.Catalog;
using Ambiquality.Public.Api.Infrastructure.Observations;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// Server-side aggregation of a quantity over a window for a building (across its sensors)
/// or a single sensor: a bucketed trend series plus an overall distribution (boxplot). Caps
/// the bucket count so an unbounded window never ships an unbounded result.
/// </summary>
public static class ObservationAggregateEndpoints
{
    /// <summary>Default window when from/to are omitted: the trailing day.</summary>
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(1);
    private const int CacheSeconds = 300;

    public static void MapObservationAggregateEndpoints(this WebApplication app)
    {
        app.MapMethods($"/{Constants.ApiVersion}/observations/aggregate", ["GET", "HEAD"], Aggregate)
            .WithTags("Observations")
            .WithName("AggregateObservations")
            .WithSummary("Aggregate observations into buckets + distribution")
            .WithDescription(
                "Server-side aggregation for the map's click-through chart. Either buildingId "
                + "(aggregate across the building's sensors) or sensorId (one sensor) is required, "
                + "plus parameterCode. Filters: from, to (ISO-8601), bucket (auto|5m|1h|6h|1d|1w). "
                + "bucket=auto picks a granularity from the span and caps the bucket count.")
            .Produces<AggregateResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);
    }

    private static async Task<IResult> Aggregate(
        HttpContext http,
        IEvidenceCatalog catalog,
        IMeasurementReader measurements,
        CancellationToken ct)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format != ResponseFormat.Json)
            return Problems.UnsupportedMediaType();

        var query = http.Request.Query;

        var parameterCode = query["parameterCode"].FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(parameterCode))
            return Problems.BadRequest(
                "Missing parameter", "The 'parameterCode' query parameter is required.", "missing-parameter");
        parameterCode = parameterCode.ToLowerInvariant();

        if (TryParseGuid(query["sensorId"].FirstOrDefault(), "sensorId", out var sensorId) is { } sensorErr)
            return sensorErr;
        if (TryParseGuid(query["buildingId"].FirstOrDefault(), "buildingId", out var buildingId) is { } buildingErr)
            return buildingErr;
        if (sensorId is null && buildingId is null)
            return Problems.BadRequest(
                "Missing target",
                "One of 'sensorId' or 'buildingId' is required.",
                "missing-target");

        if (Problems.TryParseUtcInstant(query["to"].FirstOrDefault(), "to", out var toRaw) is { } toErr)
            return toErr;
        if (Problems.TryParseUtcInstant(query["from"].FirstOrDefault(), "from", out var fromRaw) is { } fromErr)
            return fromErr;

        var to = toRaw ?? DateTime.UtcNow;
        var from = fromRaw ?? to - DefaultWindow;
        if (from > to)
            return Problems.BadRequest("Invalid range", "'from' must not be after 'to'.", "invalid-range");

        if (!AggregateBucket.TryResolve(query["bucket"].FirstOrDefault(), from, to, out var bucket))
            return Problems.BadRequest(
                "Invalid bucket",
                "The 'bucket' query parameter must be one of auto, 5m, 1h, 6h, 1d, 1w.",
                "invalid-bucket");

        // sensorId drills into one sensor; otherwise aggregate across the building's sensors.
        IReadOnlyCollection<Guid> sensorIds = sensorId is { } sid
            ? [sid]
            : await catalog.ResolveSensorIdsAsync(buildingId, null, null, ct);

        var result = await measurements.AggregateAsync(sensorIds, parameterCode, from, to, bucket.Interval, ct);

        ResponseHeaders.SetCacheHeader(http, CacheSeconds);
        return Results.Ok(new AggregateResponse(
            parameterCode, result.Unit, from, to, bucket.Label, result.Buckets, result.Stats, Constants.LicenseIri));
    }

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult? TryParseGuid(
        string? raw, string name, out Guid? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (Guid.TryParse(raw, out var guid))
        {
            value = guid;
            return null;
        }
        return Problems.BadRequest("Invalid identifier", $"The '{name}' query parameter must be a GUID.", "invalid-id");
    }
}
