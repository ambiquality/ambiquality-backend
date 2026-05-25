namespace Ambiquality.Evidence.Api.Api;

/// <summary>
/// Degrades a building's GPS coordinates on read for callers who are not its
/// owner, honouring the building's <c>anonymization_level</c>. Owners always see
/// precise coordinates; everyone else (including anonymous readers) sees them
/// coarsened — never hidden — so the open-data catalog stays usable while
/// respecting the owner's chosen precision.
/// </summary>
internal static class CoordinateMasking
{
    public static (double? Latitude, double? Longitude) Apply(
        double? latitude,
        double? longitude,
        string anonymizationCode,
        bool isOwner)
    {
        if (isOwner || latitude is null || longitude is null)
            return (latitude, longitude);

        // Decimal-place rounding: 3 dp ≈ 110 m, 2 dp ≈ 1.1 km. "precise" (and any
        // unknown code) leaves the coordinates untouched.
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
