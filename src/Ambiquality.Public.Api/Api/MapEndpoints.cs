using System.Globalization;
using Ambiquality.Public.Api.Infrastructure.Caching;
using Ambiquality.Public.Api.Infrastructure.Catalog;
using Ambiquality.Public.Api.Infrastructure.Observations;
using Microsoft.Extensions.Caching.Distributed;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// Map snapshot for the public interactive map (F18): one quantity-filtered, latest-value
/// marker per building. Anonymous, JSON-only, coordinate-masked, and Redis-cached for a short
/// window — this is the single call the map makes on load / filter change.
/// </summary>
public static class MapEndpoints
{
    /// <summary>Markers whose freshest observation is older than this are greyed out (latestValue null).</summary>
    private const int DefaultFreshnessSeconds = 900; // 15 min
    private const int CacheSeconds = 60;

    public static void MapMapEndpoints(this WebApplication app)
    {
        var group = app.MapGroup($"/{Constants.ApiVersion}/map").WithTags("Map");

        group.MapMethods("/snapshot", ["GET", "HEAD"], GetSnapshot)
            .WithName("GetMapSnapshot")
            .WithSummary("Latest-value building markers for a quantity")
            .WithDescription(
                "One marker per building with ≥1 active sensor measuring parameterCode. latestValue "
                + "is the mean of each sensor's most-recent value; stale markers (freshest observation "
                + "older than the freshness window) carry a null latestValue. Filters: parameterCode "
                + "(required), bbox (minLon,minLat,maxLon,maxLat). Cached ~60 s.")
            .Produces<MapSnapshotResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);
    }

    private static async Task<IResult> GetSnapshot(
        HttpContext http,
        IEvidenceCatalog catalog,
        IMeasurementReader measurements,
        IDistributedCache cache,
        IConfiguration config,
        CancellationToken ct)
    {
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format != ResponseFormat.Json)
            return Problems.UnsupportedMediaType();

        var parameterCode = http.Request.Query["parameterCode"].FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(parameterCode))
            return Problems.BadRequest(
                "Missing parameter", "The 'parameterCode' query parameter is required.", "missing-parameter");
        parameterCode = parameterCode.ToLowerInvariant();

        BoundingBox? bbox = null;
        var bboxRaw = http.Request.Query["bbox"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(bboxRaw))
        {
            if (!BoundingBox.TryParse(bboxRaw, out var parsed))
                return Problems.InvalidBbox();
            bbox = parsed;
        }

        var freshness = TimeSpan.FromSeconds(
            config.GetValue<int?>("PublicApi:MapSnapshotFreshnessSeconds") ?? DefaultFreshnessSeconds);

        var key = CacheKey(parameterCode, bbox);
        var response = await JsonDistributedCache.GetOrCreateAsync(
            cache, key, TimeSpan.FromSeconds(CacheSeconds),
            token => ComputeSnapshotAsync(catalog, measurements, parameterCode, bbox, freshness, token), ct);

        ResponseHeaders.SetCacheHeader(http, CacheSeconds);
        return Results.Ok(response);
    }

    private static async Task<MapSnapshotResponse> ComputeSnapshotAsync(
        IEvidenceCatalog catalog,
        IMeasurementReader measurements,
        string parameterCode,
        BoundingBox? bbox,
        TimeSpan freshness,
        CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var buildings = await catalog.GetMapBuildingsAsync(parameterCode, bbox, ct);

        var allSensorIds = buildings.SelectMany(b => b.SensorIds).Distinct().ToList();
        var latest = await measurements.GetLatestPerSensorAsync(allSensorIds, parameterCode, ct);
        var bySensor = latest.ToDictionary(l => l.SensorId);

        var items = new List<MapSnapshotItem>(buildings.Count);
        string? unit = null;
        foreach (var b in buildings)
        {
            var contributing = b.SensorIds
                .Select(id => bySensor.TryGetValue(id, out var obs) ? obs : null)
                .Where(obs => obs is not null)
                .Select(obs => obs!)
                .ToList();

            DateTime? observedAt = contributing.Count > 0 ? contributing.Max(o => o.ObservedAt) : null;
            var stale = observedAt is null || asOf - observedAt.Value > freshness;
            double? latestValue = stale ? null : contributing.Average(o => o.Value);

            unit ??= contributing.FirstOrDefault(o => o.Unit is not null)?.Unit;

            items.Add(new MapSnapshotItem(
                BuildingId: b.Id,
                Slug: b.Slug,
                Name: b.Name,
                Lat: b.Latitude,
                Lon: b.Longitude,
                LatestValue: latestValue,
                ObservedAt: observedAt,
                Stale: stale,
                SensorCount: b.SensorIds.Count));
        }

        return new MapSnapshotResponse(parameterCode, unit, asOf, items, Constants.LicenseIri);
    }

    /// <summary>
    /// Cache key over the quantity and a coarsened bbox: rounding to ~3 dp (≈110 m) lets
    /// near-identical viewports share a cached snapshot without leaking finer precision.
    /// </summary>
    private static string CacheKey(string parameterCode, BoundingBox? bbox)
    {
        if (bbox is not { } b)
            return $"map:snapshot:{parameterCode}";
        static string R(double v) => Math.Round(v, 3).ToString(CultureInfo.InvariantCulture);
        return $"map:snapshot:{parameterCode}:{R(b.MinLon)},{R(b.MinLat)},{R(b.MaxLon)},{R(b.MaxLat)}";
    }
}
