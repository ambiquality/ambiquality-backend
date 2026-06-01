namespace Ambiquality.Public.Api.Api;

/// <summary>
/// Coarsens a building's GPS coordinates for the public (always non-owner) reader,
/// honouring the building's <c>anonymization</c> code. Coordinates are degraded —
/// never hidden — so the open-data catalog stays usable while respecting the owner's
/// chosen precision. Mirrors <c>Evidence.Api/Api/CoordinateMasking.cs</c>; copied
/// here because Public.Api has no other Evidence.Api dependency.
/// </summary>
internal static class CoordinateMasking
{
    /// <summary>
    /// Applies the anonymization rule. Public.Api is always anonymous, so masking
    /// is unconditional: <c>street</c> ≈ 110 m (3 dp), <c>municipality</c> ≈ 1.1 km
    /// (2 dp); <c>precise</c> and any unknown code leave coordinates untouched.
    /// </summary>
    public static (double? Latitude, double? Longitude) Apply(
        double? latitude, double? longitude, string? anonymizationCode)
    {
        if (latitude is null || longitude is null)
            return (latitude, longitude);

        int? decimals = anonymizationCode switch
        {
            "street" => 3,
            "municipality" => 2,
            _ => null
        };

        if (decimals is null)
            return (latitude, longitude);

        return (Math.Round(latitude.Value, decimals.Value),
                Math.Round(longitude.Value, decimals.Value));
    }
}
