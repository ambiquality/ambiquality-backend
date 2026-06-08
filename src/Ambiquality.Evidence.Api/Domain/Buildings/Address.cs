using System.Globalization;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>
/// Czech postal address of a building, modelled after the OFN "Adresy"
/// (2020-07-01) open formal standard. The RÚIAN address-point code
/// (<c>kód_adresního_místa</c>) is the canonical anchor; the structured
/// components are stored alongside it so the public projection can render the
/// OFN <c>Adresa</c> node — and its free-text form — without a live RÚIAN
/// lookup. The country is implicitly the Czech Republic (the platform is
/// CZ-only). All parts co-vary inside <c>building_address_history</c>.
/// </summary>
public sealed record Address(
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
    string? RegionName)
{
    /// <summary>č.p. — číslo popisné (descriptive number).</summary>
    public const string HouseNumberTypeDescriptive = "č.p.";

    /// <summary>č.ev. — číslo evidenční (registration number).</summary>
    public const string HouseNumberTypeRegistration = "č.ev.";

    /// <summary>
    /// Validates and normalises the OFN address components. <paramref name="streetName"/>
    /// is optional (small municipalities have no street names — the house number alone
    /// identifies the address); so are the orientation number, municipal part, district
    /// (okres) and region (kraj).
    /// </summary>
    public static Address Create(
        long addressPointCode,
        string? streetName,
        int houseNumber,
        string houseNumberType,
        int? orientationNumber,
        string? orientationNumberLetter,
        string municipalityName,
        string? municipalityPartName,
        string psc,
        string? districtName,
        string? regionName)
    {
        if (addressPointCode <= 0)
            throw new ArgumentException(
                "RÚIAN address-point code (kód adresního místa) must be a positive number.",
                nameof(addressPointCode));
        if (houseNumber <= 0)
            throw new ArgumentException(
                "House number (číslo domovní) must be positive.", nameof(houseNumber));
        if (orientationNumber is <= 0)
            throw new ArgumentException(
                "Orientation number (číslo orientační) must be positive when present.",
                nameof(orientationNumber));
        if (string.IsNullOrWhiteSpace(municipalityName))
            throw new ArgumentException(
                "Municipality name (název obce) cannot be empty.", nameof(municipalityName));

        return new Address(
            addressPointCode,
            Trim(streetName),
            houseNumber,
            NormalizeHouseNumberType(houseNumberType),
            orientationNumber,
            Trim(orientationNumberLetter),
            municipalityName.Trim(),
            Trim(municipalityPartName),
            NormalizePsc(psc),
            Trim(districtName),
            Trim(regionName));
    }

    /// <summary>Dereferenceable RÚIAN address-point IRI (OFN <c>adresní_místo</c>).</summary>
    public string AddressPointIri =>
        $"https://linked.cuzk.cz/resource/ruian/adresni-misto/{AddressPointCode}";

    /// <summary>The address-point code as the OFN <c>kód_adresního_místa</c> text value.</summary>
    public string AddressPointCodeText =>
        AddressPointCode.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Composes the OFN free-text address (<c>text</c>) per Czech postal convention,
    /// e.g. "Nám. W. Churchilla 1938/4, 130 67 Praha". When there is no street name the
    /// municipal part (or the municipality) carries the house number.
    /// </summary>
    public string ToText()
    {
        var house = OrientationNumber is null
            ? HouseNumber.ToString(CultureInfo.InvariantCulture)
            : $"{HouseNumber}/{OrientationNumber}{OrientationNumberLetter}";
        var locality = StreetName ?? MunicipalityPartName ?? MunicipalityName;
        return $"{locality} {house}, {FormatPsc(Psc)} {MunicipalityName}";
    }

    /// <summary>Formats a 5-digit PSČ in the conventional "NNN NN" grouping.</summary>
    public static string FormatPsc(string psc) =>
        psc.Length == 5 ? $"{psc[..3]} {psc[3..]}" : psc;

    private static string NormalizeHouseNumberType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "House-number type (typ čísla domovního) cannot be empty.", nameof(value));
        var trimmed = value.Trim();
        if (trimmed is HouseNumberTypeDescriptive or HouseNumberTypeRegistration)
            return trimmed;
        throw new ArgumentException(
            $"House-number type must be '{HouseNumberTypeDescriptive}' or '{HouseNumberTypeRegistration}'.",
            nameof(value));
    }

    private static string NormalizePsc(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Postal code (PSČ) cannot be empty.", nameof(value));
        var digits = value.Replace(" ", string.Empty).Trim();
        if (digits.Length != 5 || !digits.All(char.IsAsciiDigit))
            throw new ArgumentException("Postal code (PSČ) must be five digits.", nameof(value));
        return digits;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
