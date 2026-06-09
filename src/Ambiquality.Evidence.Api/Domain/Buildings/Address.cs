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
/// <remarks>
/// OFN models the territorial elements (ulice, obec, část obce, okres, kraj/VÚSC)
/// as dereferenceable RÚIAN IRIs, not just labels. The <c>*Code</c> fields carry
/// the RÚIAN codes that back those IRIs; the parallel <c>*Name</c> fields are the
/// supplementary human-readable <c>název_*</c> labels. Every code is optional — a
/// bare <c>adresní_místo</c> IRI already identifies the address completely.
/// </remarks>
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
    string? RegionName,
    long? StreetCode = null,
    long? MunicipalityCode = null,
    long? MunicipalityPartCode = null,
    long? DistrictCode = null,
    long? RegionCode = null)
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
        string? regionName,
        long? streetCode = null,
        long? municipalityCode = null,
        long? municipalityPartCode = null,
        long? districtCode = null,
        long? regionCode = null)
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
        EnsurePositiveCode(streetCode, "Street (ulice)", nameof(streetCode));
        EnsurePositiveCode(municipalityCode, "Municipality (obec)", nameof(municipalityCode));
        EnsurePositiveCode(municipalityPartCode, "Municipality part (část obce)", nameof(municipalityPartCode));
        EnsurePositiveCode(districtCode, "District (okres)", nameof(districtCode));
        EnsurePositiveCode(regionCode, "Region (VÚSC)", nameof(regionCode));

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
            Trim(regionName),
            streetCode,
            municipalityCode,
            municipalityPartCode,
            districtCode,
            regionCode);
    }

    /// <summary>Dereferenceable RÚIAN address-point IRI (OFN <c>adresní_místo</c>).</summary>
    public string AddressPointIri =>
        $"https://linked.cuzk.cz/resource/ruian/adresni-misto/{AddressPointCode}";

    /// <summary>RÚIAN street IRI (OFN <c>ulice</c>), or null when no street code is recorded.</summary>
    public string? StreetIri => RuianIri("ulice", StreetCode);

    /// <summary>RÚIAN municipality IRI (OFN <c>obec</c>), or null when no obec code is recorded.</summary>
    public string? MunicipalityIri => RuianIri("obec", MunicipalityCode);

    /// <summary>RÚIAN municipality-part IRI (OFN <c>část_obce</c>), or null when not recorded.</summary>
    public string? MunicipalityPartIri => RuianIri("cast-obce", MunicipalityPartCode);

    /// <summary>RÚIAN district IRI (OFN <c>okres</c>), or null when no okres code is recorded.</summary>
    public string? DistrictIri => RuianIri("okres", DistrictCode);

    /// <summary>RÚIAN region IRI (OFN <c>vúsc</c> — kraj), or null when no VÚSC code is recorded.</summary>
    public string? RegionIri => RuianIri("vusc", RegionCode);

    private static string? RuianIri(string segment, long? code) =>
        code is { } c ? $"https://linked.cuzk.cz/resource/ruian/{segment}/{c.ToString(CultureInfo.InvariantCulture)}" : null;

    private static void EnsurePositiveCode(long? code, string label, string paramName)
    {
        if (code is <= 0)
            throw new ArgumentException(
                $"{label} RÚIAN code must be a positive number when present.", paramName);
    }

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
