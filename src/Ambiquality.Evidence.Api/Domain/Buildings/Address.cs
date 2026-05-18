namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>
/// Postal address of a building. All four parts are required because they
/// co-vary inside <c>building_address_history</c>; the country is normalised
/// to upper-invariant for stable equality.
/// </summary>
public sealed record Address(string Street, string City, string Postcode, string Country)
{
    public static Address Create(string street, string city, string postcode, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
            throw new ArgumentException("Street cannot be empty.", nameof(street));
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City cannot be empty.", nameof(city));
        if (string.IsNullOrWhiteSpace(postcode))
            throw new ArgumentException("Postcode cannot be empty.", nameof(postcode));
        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country cannot be empty.", nameof(country));

        return new Address(
            street.Trim(),
            city.Trim(),
            postcode.Trim(),
            country.Trim().ToUpperInvariant());
    }
}
