using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Buildings;

namespace Ambiquality.Evidence.Api.Infrastructure.Ruian;

/// <summary>
/// <see cref="IAddressGeocoder"/> backed by the ČÚZK RÚIAN ArcGIS service
/// (<c>…/RUIAN/MapServer</c>, open data, CC BY 4.0). The Esri <c>GeocodeSOE</c> locator only
/// returns a display string + an opaque <c>magicKey</c> — the RÚIAN codes and structured
/// components live on the <c>MapServer</c> feature layers, so <see cref="ResolveAsync"/> reads
/// the address point (layer 1) and then enriches it from the street and territorial layers
/// (ulice / obec / část obce / okres / VÚSC) to compose the full OFN <c>Adresy</c> address.
/// </summary>
/// <remarks>
/// The <c>magicKey</c> a suggestion carries is <c>"&lt;locatorLayer&gt;_&lt;objectid&gt;"</c>; the
/// suffix is the <c>AdresniMisto</c> layer object id, which we resolve directly. Coordinates are
/// requested in WGS84 (<c>outSR=4326</c>), so the geometry <c>x</c>/<c>y</c> are longitude/latitude.
/// </remarks>
public sealed class RuianGeocoderClient(HttpClient http) : IAddressGeocoder
{
    // RÚIAN MapServer layer ids (see …/RUIAN/MapServer?f=json).
    private const int AddressPointLayer = 1;   // AdresniMisto
    private const int StreetLayer = 4;          // Ulice
    private const int MunicipalityPartLayer = 11; // CastObce
    private const int MunicipalityLayer = 12;   // Obec
    private const int DistrictLayer = 15;       // Okres
    private const int RegionLayer = 17;         // VyssiUzemneSamospravnyCelek (kraj)

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(
        string query, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return [];

        var url = $"exts/GeocodeSOE/suggest?text={Uri.EscapeDataString(query.Trim())}&f=json";
        var dto = await GetAsync<SuggestDto>(url, cancellationToken);

        return (dto.Suggestions ?? [])
            // Only address points expand to a complete building address; streets, municipalities
            // and other layers are not a postal address and cannot fill the OFN form.
            .Where(s => string.Equals(s.Type, "AdresniMisto", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(s.Text) && !string.IsNullOrWhiteSpace(s.MagicKey))
            .Take(Math.Clamp(limit, 1, 25))
            .Select(s => new AddressSuggestion(s.Text!, s.MagicKey!))
            .ToList();
    }

    public async Task<ResolvedAddress?> ResolveAsync(string key, CancellationToken cancellationToken)
    {
        var objectId = ParseObjectId(key);
        if (objectId is null)
            return null;

        var point = await QueryFirstAsync(
            $"{AddressPointLayer}/query?objectIds={objectId}" +
            "&outFields=kod,cislodomovni,cisloorientacni,cisloorientacnipismeno,psc,ulice" +
            "&returnGeometry=true&outSR=4326&f=json",
            cancellationToken);
        if (point?.Attributes is not { } a)
            return null;

        var addressPointCode = GetLong(a, "kod");
        var houseNumber = GetLong(a, "cislodomovni");
        var psc = GetLong(a, "psc");
        if (addressPointCode is null or <= 0 || houseNumber is null or <= 0 || psc is null)
            return null; // a usable address point always carries these; bail rather than guess.

        var streetCode = GetLong(a, "ulice");
        var longitude = point.Geometry?.X;
        var latitude = point.Geometry?.Y;
        var pointQuery = latitude is { } lat && longitude is { } lon
            ? $"geometry={Fmt(lon)},{Fmt(lat)}&geometryType=esriGeometryPoint&inSR=4326" +
              "&spatialRel=esriSpatialRelIntersects&returnGeometry=false&outFields=kod,nazev&f=json"
            : null;

        // The street name comes by code (Ulice is a line layer, so a point-intersect misses it);
        // the territorial elements come by the address point's location. Fan out in parallel.
        var streetTask = streetCode is { } sc
            ? QueryFirstAsync($"{StreetLayer}/query?where=kod={sc}&returnGeometry=false&outFields=kod,nazev&f=json", cancellationToken)
            : Task.FromResult<FeatureDto?>(null);
        var municipalityTask = QueryByPointAsync(MunicipalityLayer, pointQuery, cancellationToken);
        var municipalityPartTask = QueryByPointAsync(MunicipalityPartLayer, pointQuery, cancellationToken);
        var districtTask = QueryByPointAsync(DistrictLayer, pointQuery, cancellationToken);
        var regionTask = QueryByPointAsync(RegionLayer, pointQuery, cancellationToken);
        await Task.WhenAll(streetTask, municipalityTask, municipalityPartTask, districtTask, regionTask);

        var street = streetTask.Result;
        var municipality = municipalityTask.Result;
        var municipalityPart = municipalityPartTask.Result;
        var district = districtTask.Result;
        var region = regionTask.Result;

        // Reuse the domain rules (validation + PSČ/number-type normalisation + free-text form), so a
        // resolved address is shaped exactly like one the registration command will accept.
        var address = Address.Create(
            addressPointCode: addressPointCode.Value,
            streetName: Name(street),
            houseNumber: (int)houseNumber.Value,
            houseNumberType: Address.HouseNumberTypeDescriptive,
            orientationNumber: (int?)GetLong(a, "cisloorientacni"),
            orientationNumberLetter: GetString(a, "cisloorientacnipismeno"),
            municipalityName: Name(municipality) ?? string.Empty,
            municipalityPartName: Name(municipalityPart),
            psc: psc.Value.ToString("D5", CultureInfo.InvariantCulture),
            districtName: Name(district),
            regionName: Name(region),
            streetCode: streetCode,
            municipalityCode: Code(municipality),
            municipalityPartCode: Code(municipalityPart),
            districtCode: Code(district),
            regionCode: Code(region));

        return new ResolvedAddress(
            address.AddressPointCode,
            address.StreetName,
            address.HouseNumber,
            address.HouseNumberType,
            address.OrientationNumber,
            address.OrientationNumberLetter,
            address.MunicipalityName,
            address.MunicipalityPartName,
            address.Psc,
            address.DistrictName,
            address.RegionName,
            address.StreetCode,
            address.MunicipalityCode,
            address.MunicipalityPartCode,
            address.DistrictCode,
            address.RegionCode,
            latitude,
            longitude,
            address.ToText());
    }

    private Task<FeatureDto?> QueryByPointAsync(int layer, string? pointQuery, CancellationToken ct) =>
        pointQuery is null
            ? Task.FromResult<FeatureDto?>(null)
            : QueryFirstAsync($"{layer}/query?{pointQuery}", ct);

    private async Task<FeatureDto?> QueryFirstAsync(string url, CancellationToken cancellationToken)
    {
        var dto = await GetAsync<QueryDto>(url, cancellationToken);
        if (dto.Error is { } error)
            throw new InvalidOperationException(
                $"RÚIAN query failed ({error.Code}): {error.Message}");
        return dto.Features is { Count: > 0 } features ? features[0] : null;
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return value ?? throw new InvalidOperationException($"RÚIAN returned an empty body for '{url}'.");
    }

    /// <summary>Extracts the <c>AdresniMisto</c> object id from a <c>magicKey</c> (<c>"1_555742"</c>)
    /// or accepts a bare numeric key. Returns <c>null</c> for an unparseable key.</summary>
    private static long? ParseObjectId(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;
        var token = key.Contains('_') ? key[(key.LastIndexOf('_') + 1)..] : key;
        return long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : null;
    }

    private static string Fmt(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? Name(FeatureDto? feature) => GetString(feature?.Attributes, "nazev");

    private static long? Code(FeatureDto? feature) => GetLong(feature?.Attributes, "kod");

    private static long? GetLong(IReadOnlyDictionary<string, JsonElement>? attrs, string key)
    {
        if (attrs is null || !attrs.TryGetValue(key, out var element))
            return null;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out var n) ? n : null,
            JsonValueKind.String when long.TryParse(
                element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) => s,
            _ => null,
        };
    }

    private static string? GetString(IReadOnlyDictionary<string, JsonElement>? attrs, string key)
    {
        if (attrs is null || !attrs.TryGetValue(key, out var element) || element.ValueKind != JsonValueKind.String)
            return null;
        var value = element.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record SuggestDto(List<SuggestionDto>? Suggestions);

    private sealed record SuggestionDto(string? Text, string? MagicKey, string? Type);

    private sealed record QueryDto(List<FeatureDto>? Features, ErrorDto? Error);

    private sealed record FeatureDto(
        [property: JsonPropertyName("attributes")] Dictionary<string, JsonElement>? Attributes,
        [property: JsonPropertyName("geometry")] GeometryDto? Geometry);

    private sealed record GeometryDto(double X, double Y);

    private sealed record ErrorDto(int Code, string? Message);
}
