namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>
/// WGS-84 latitude / longitude pair. Stored as raw precise values; the
/// <see cref="Common.AnonymizationLevel"/> on the same history row controls
/// what is exposed publicly.
/// </summary>
public sealed record Coordinates(double Latitude, double Longitude)
{
    public static Coordinates Create(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude < -90.0 || latitude > 90.0)
            throw new ArgumentException("Latitude must be a real number in [-90, 90].", nameof(latitude));
        if (double.IsNaN(longitude) || longitude < -180.0 || longitude > 180.0)
            throw new ArgumentException("Longitude must be a real number in [-180, 180].", nameof(longitude));

        return new Coordinates(latitude, longitude);
    }
}
