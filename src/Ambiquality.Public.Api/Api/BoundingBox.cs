using System.Globalization;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// A geographic bounding box parsed from the <c>bbox</c> query parameter, given as
/// four comma-separated decimals <c>minLon,minLat,maxLon,maxLat</c> (the GeoJSON /
/// OGC axis order). Longitude ∈ [-180,180], latitude ∈ [-90,90], mins ≤ maxes.
/// </summary>
public readonly record struct BoundingBox(double MinLon, double MinLat, double MaxLon, double MaxLat)
{
    public static bool TryParse(string? raw, out BoundingBox bbox)
    {
        bbox = default;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var parts = raw.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
            return false;

        var values = new double[4];
        for (var i = 0; i < 4; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                return false;
        }

        var (minLon, minLat, maxLon, maxLat) = (values[0], values[1], values[2], values[3]);

        if (minLon > maxLon || minLat > maxLat)
            return false;
        if (minLon < -180 || maxLon > 180 || minLat < -90 || maxLat > 90)
            return false;

        bbox = new BoundingBox(minLon, minLat, maxLon, maxLat);
        return true;
    }
}
