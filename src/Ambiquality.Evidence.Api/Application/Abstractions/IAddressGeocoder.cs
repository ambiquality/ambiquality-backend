namespace Ambiquality.Evidence.Api.Application.Abstractions;

/// <summary>
/// Resolves Czech addresses against an external RÚIAN geocoder so the building
/// registration form can be filled from a picked suggestion instead of the operator
/// hand-copying the OFN <c>Adresy</c> components out of the registry. The address-lookup
/// endpoints are a thin read-through over this abstraction; the concrete implementation
/// (ČÚZK ArcGIS) lives in <c>Infrastructure/Ruian</c>. Evidence.Api stays authoritative —
/// a resolved address is only a convenience that the registration command re-validates.
/// </summary>
public interface IAddressGeocoder
{
    /// <summary>
    /// Returns address-point suggestions for the free-text <paramref name="query"/> (for
    /// autocomplete). Each suggestion carries an opaque <see cref="AddressSuggestion.Key"/>
    /// to pass back to <see cref="ResolveAsync"/>. Returns an empty list for a blank/too-short
    /// query or no matches.
    /// </summary>
    Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(
        string query, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a suggestion <paramref name="key"/> to the full OFN address (structured
    /// components + RÚIAN codes + WGS84 coordinates), or <c>null</c> when the key matches
    /// no address point. Throws on an upstream transport/format failure so the endpoint can
    /// surface a <c>502</c> rather than a fabricated result.
    /// </summary>
    Task<ResolvedAddress?> ResolveAsync(string key, CancellationToken cancellationToken);
}

/// <summary>A single autocomplete candidate: the display <paramref name="Text"/> and the opaque
/// <paramref name="Key"/> that <see cref="IAddressGeocoder.ResolveAsync"/> expands.</summary>
public sealed record AddressSuggestion(string Text, string Key);

/// <summary>
/// A fully resolved Czech OFN <c>Adresy</c> address. Fields mirror the building
/// <see cref="Domain.Buildings.Address"/> record one-to-one (so the registration form maps
/// straight onto them), plus the address-point WGS84 coordinates and the composed free-text form.
/// </summary>
public sealed record ResolvedAddress(
    long AddressPointCode,
    string? StreetName,
    int HouseNumber,
    string HouseNumberType,
    int? OrientationNumber,
    string? OrientationNumberLetter,
    string MunicipalityName,
    string? MunicipalityPartName,
    string Psc,
    string? DistrictName,
    string? RegionName,
    long? StreetCode,
    long? MunicipalityCode,
    long? MunicipalityPartCode,
    long? DistrictCode,
    long? RegionCode,
    double? Latitude,
    double? Longitude,
    string Text);
