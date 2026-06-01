namespace Ambiquality.Public.Api.Api;

/// <summary>
/// Builds the absolute, dereferenceable IRIs that anchor the linked-data
/// responses (@id values, next-page links, schema/context references).
/// </summary>
/// <remarks>
/// The base is resolved per request:
/// <list type="number">
///   <item>the configured <c>PublicApi:BaseIri</c> override when set (preferred for
///   deployments behind a path-rewriting proxy such as Caddy's <c>handle_path
///   /public/*</c>, where the request the app sees has lost the public prefix);</item>
///   <item>otherwise derived from the request, honouring <c>X-Forwarded-Proto</c>,
///   <c>X-Forwarded-Host</c> and <c>X-Forwarded-Prefix</c> / <c>PathBase</c>.</item>
/// </list>
/// All resource helpers hang off the versioned <c>/v1</c> root.
/// </remarks>
public sealed class IriBuilder
{
    private readonly string _root; // absolute, no trailing slash, includes "/v1"

    private IriBuilder(string root) => _root = root;

    public static IriBuilder ForRequest(HttpRequest request, IConfiguration configuration)
    {
        var configured = configuration["PublicApi:BaseIri"];

        string origin;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            origin = configured.TrimEnd('/');
        }
        else
        {
            var scheme = First(request.Headers["X-Forwarded-Proto"]) ?? request.Scheme;
            var host = First(request.Headers["X-Forwarded-Host"]) ?? request.Host.Value;
            var prefix = First(request.Headers["X-Forwarded-Prefix"])
                ?? (request.PathBase.HasValue ? request.PathBase.Value : string.Empty);
            origin = $"{scheme}://{host}{prefix?.TrimEnd('/')}";
        }

        var root = origin.EndsWith($"/{Constants.ApiVersion}", StringComparison.Ordinal)
            ? origin
            : $"{origin}/{Constants.ApiVersion}";

        return new IriBuilder(root);
    }

    private static string? First(Microsoft.Extensions.Primitives.StringValues values)
    {
        var v = values.Count > 0 ? values[0] : null;
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    /// <summary>The versioned API root, e.g. <c>https://data.ambiquality.org/v1</c>.</summary>
    public string Root => _root;

    public string Observations() => $"{_root}/observations";

    public string Observation(Guid id) => $"{_root}/observations/{id:D}";

    public string ObservationsCsv() => $"{_root}/observations.csv";

    public string Buildings() => $"{_root}/buildings";

    public string Building(Guid id) => $"{_root}/buildings/{id:D}";

    public string BuildingRooms(Guid buildingId) => $"{_root}/buildings/{buildingId:D}/rooms";

    public string Room(Guid id) => $"{_root}/rooms/{id:D}";

    public string RoomSensors(Guid roomId) => $"{_root}/rooms/{roomId:D}/sensors";

    public string Sensor(Guid id) => $"{_root}/sensors/{id:D}";

    public string Catalog() => $"{_root}/catalog";

    public string Context() => $"{_root}/context/measurements.jsonld";

    public string Schema(string resource) => $"{_root}/schema/{resource}.json";

    /// <summary>The CSVW tabular-schema document that describes the observations CSV.</summary>
    public string CsvMetadata() => $"{_root}/schema/observations.csv-metadata.json";

    /// <summary>Re-emits the observations list IRI carrying an opaque next-page cursor.</summary>
    public string ObservationsNext(string cursor, string? query) =>
        $"{Observations()}?{(string.IsNullOrEmpty(query) ? string.Empty : query + "&")}cursor={Uri.EscapeDataString(cursor)}";
}
