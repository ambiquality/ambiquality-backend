namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>
/// WGS-84 latitude / longitude pair. Stored as raw precise values and exposed
/// as-is on the public open-data API.
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
