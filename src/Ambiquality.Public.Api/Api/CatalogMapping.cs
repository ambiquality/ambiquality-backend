using System.Globalization;
using Ambiquality.Core.Domain.Vocabulary;
using Ambiquality.Public.Api.Infrastructure.Catalog;

namespace Ambiquality.Public.Api.Api;

/// <summary>Maps raw evidence rows to public contracts (masking + IRIs + QUDT).</summary>
internal static class CatalogMappers
{
    public static BuildingResponse ToResponse(BuildingRow b, IriBuilder iri)
    {
        var address = new AddressDto(
            b.AddressPointCode,
            b.StreetName,
            b.HouseNumber,
            b.HouseNumberType,
            b.OrientationNumber,
            b.OrientationNumberLetter,
            b.MunicipalityName,
            b.MunicipalityPartName,
            b.Psc,
            b.DistrictName,
            b.RegionName);
        return new BuildingResponse(
            b.Id, iri.Building(b.Id), b.Name,
            address, b.BuildingTypeCode, b.Latitude, b.Longitude, b.YearBuilt, b.YearRenovated, Constants.LicenseIri);
    }

    public static RoomResponse ToResponse(RoomRow r, IriBuilder iri) => new(
        r.Id, iri.Room(r.Id), r.BuildingId, r.Name, r.Floor, r.FunctionCode, r.ExposureCode,
        r.AreaM2, r.CeilingHeightM, r.VentilationType, r.PollutionSources, Constants.LicenseIri);

    public static SensorResponse ToResponse(SensorRow s, IriBuilder iri) => new(
        s.Id, iri.Sensor(s.Id), s.BuildingId, s.RoomId, s.Manufacturer, s.Model, s.SerialNumber,
        s.StatusCode, s.MeasuredParameterCodes.Select(MeasuredParameterDto.FromCode).ToList(),
        Constants.LicenseIri);
}

/// <summary>
/// JSON-LD projections of catalog entities. Each carries an inline <c>@context</c>
/// (the catalog vocabulary is small and self-contained, unlike observations which
/// reference the shared measurements context). Sensors are typed as <c>sosa:Sensor</c>
/// and expose their observable quantity kinds via <c>sosa:observes</c>.
/// </summary>
internal static class CatalogJsonLd
{
    private static readonly IReadOnlyDictionary<string, object?> Context = new Dictionary<string, object?>
    {
        ["ambiq"] = Constants.AmbiqNamespace,
        ["sosa"] = Constants.SosaNamespace,
        ["qudt"] = Constants.QudtSchemaNamespace,
        ["quantitykind"] = Constants.QudtQuantityKindBase,
        ["unit"] = Constants.QudtUnitBase,
        ["skos"] = Constants.SkosNamespace,
        ["dcterms"] = Constants.DctermsNamespace,
        ["license"] = new Dictionary<string, object?> { ["@id"] = "dcterms:license", ["@type"] = "@id" }
    };

    // A code attribute as a reference to its SKOS codelist concept (dereferenceable at
    // /v1/codelists/{scheme}/{code}). Falls back to the bare string for any legacy value
    // that predates the controlled vocabulary, so old data still serialises.
    private static object? Concept(IriBuilder iri, Codelist codelist, string? code) =>
        code is null ? null
        : codelist.IsValid(code)
            ? new Dictionary<string, object?>
            {
                ["@id"] = iri.CodelistConcept(codelist.Scheme, code),
                ["skos:notation"] = code
            }
            : code;

    // The OFN "Adresy" (2020-07-01) JSON-LD context. The building's address is emitted
    // as a nested, scoped-context Adresa node so consumers see a standard Czech address.
    private const string OfnAddressContext = "https://ofn.gov.cz/adresy/2020-07-01/kontexty/adresa.jsonld";

    public static IReadOnlyDictionary<string, object?> ToBuilding(BuildingResponse b, IriBuilder iri) => new Dictionary<string, object?>
    {
        ["@context"] = Context,
        ["@id"] = b.Iri,
        ["@type"] = "ambiq:Building",
        ["ambiq:name"] = b.Name,
        ["ambiq:address"] = OfnAddress(b.Address),
        ["ambiq:buildingType"] = Concept(iri, Codelists.BuildingType, b.BuildingTypeCode),
        ["ambiq:latitude"] = b.Latitude,
        ["ambiq:longitude"] = b.Longitude,
        ["ambiq:yearBuilt"] = b.YearBuilt,
        ["ambiq:yearRenovated"] = b.YearRenovated,
        ["license"] = b.License
    };

    /// <summary>
    /// Builds an OFN <c>Adresa</c> node (with its own scoped <c>@context</c>) from the
    /// address DTO. Prefers the RÚIAN <c>adresní_místo</c> IRI, then the structured Czech
    /// components, and always carries the composed free-text <c>text</c>. Returns
    /// <c>null</c> when the building has no recorded address.
    /// </summary>
    private static IReadOnlyDictionary<string, object?>? OfnAddress(AddressDto a)
    {
        if (a.AddressPointCode is null && a.MunicipalityName is null)
            return null;

        var node = new Dictionary<string, object?>
        {
            ["@context"] = OfnAddressContext,
            ["typ"] = "Adresa"
        };

        if (a.AddressPointCode is { } code)
        {
            node["adresní_místo"] = $"https://linked.cuzk.cz/resource/ruian/adresni-misto/{code.ToString(CultureInfo.InvariantCulture)}";
            node["kód_adresního_místa"] = code.ToString(CultureInfo.InvariantCulture);
        }

        AddText(node, "název_ulice", a.StreetName);
        if (a.HouseNumber is { } houseNumber) node["číslo_domovní"] = houseNumber;
        AddText(node, "typ_čísla_domovního", a.HouseNumberType);
        if (a.OrientationNumber is { } orientationNumber) node["číslo_orientační"] = orientationNumber;
        AddText(node, "znak_čísla_orientačního", a.OrientationNumberLetter);
        AddText(node, "název_obce", a.MunicipalityName);
        AddText(node, "název_části_obce", a.MunicipalityPartName);
        AddText(node, "název_okresu", a.DistrictName);
        AddText(node, "název_vúsc", a.RegionName);
        AddText(node, "psč", a.Psc);

        var text = ComposeAddressText(a);
        if (text is not null)
            node["text"] = new Dictionary<string, object?> { ["cs"] = text };

        return node;
    }

    private static void AddText(IDictionary<string, object?> node, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value)) node[key] = value;
    }

    /// <summary>Composes the OFN free-text address per Czech postal convention.</summary>
    private static string? ComposeAddressText(AddressDto a)
    {
        if (a.HouseNumber is null || a.MunicipalityName is null)
            return null;

        var house = a.OrientationNumber is { } orientationNumber
            ? $"{a.HouseNumber}/{orientationNumber}{a.OrientationNumberLetter}"
            : a.HouseNumber.Value.ToString(CultureInfo.InvariantCulture);
        var locality = a.StreetName ?? a.MunicipalityPartName ?? a.MunicipalityName;
        var psc = a.Psc is { Length: 5 } p ? $"{p[..3]} {p[3..]}" : a.Psc;
        return $"{locality} {house}, {psc} {a.MunicipalityName}";
    }

    public static IReadOnlyDictionary<string, object?> ToRoom(RoomResponse r, IriBuilder iri) => new Dictionary<string, object?>
    {
        ["@context"] = Context,
        ["@id"] = r.Iri,
        ["@type"] = "ambiq:Room",
        ["ambiq:building"] = new Dictionary<string, object?> { ["@id"] = r.BuildingId.ToString("D") },
        ["ambiq:name"] = r.Name,
        ["ambiq:floor"] = r.Floor,
        ["ambiq:function"] = Concept(iri, Codelists.RoomFunction, r.FunctionCode),
        ["ambiq:exposure"] = Concept(iri, Codelists.Exposure, r.ExposureCode),
        ["ambiq:areaM2"] = r.AreaM2,
        ["ambiq:ceilingHeightM"] = r.CeilingHeightM,
        ["ambiq:ventilationType"] = Concept(iri, Codelists.VentilationType, r.VentilationType),
        ["ambiq:pollutionSources"] = r.PollutionSources.Select(s => Concept(iri, Codelists.PollutionSource, s)).ToList(),
        ["license"] = r.License
    };

    public static IReadOnlyDictionary<string, object?> ToSensor(SensorResponse s, IriBuilder iri) => new Dictionary<string, object?>
    {
        ["@context"] = Context,
        ["@id"] = s.Iri,
        ["@type"] = "sosa:Sensor",
        ["ambiq:building"] = new Dictionary<string, object?> { ["@id"] = s.BuildingId.ToString("D") },
        ["ambiq:room"] = new Dictionary<string, object?> { ["@id"] = s.RoomId.ToString("D") },
        ["ambiq:manufacturer"] = s.Manufacturer,
        ["ambiq:model"] = s.Model,
        ["ambiq:serialNumber"] = s.SerialNumber,
        ["ambiq:status"] = Concept(iri, Codelists.SensorStatus, s.StatusCode),
        ["sosa:observes"] = s.MeasuredParameters
            .Where(p => p.QuantityKindUri is not null)
            .Select(p => new Dictionary<string, object?> { ["@id"] = p.QuantityKindUri })
            .ToList(),
        ["license"] = s.License
    };

    public static IReadOnlyDictionary<string, object?> ToGraph(IEnumerable<IReadOnlyDictionary<string, object?>> members)
    {
        // Members already include @context; strip it from each and hoist once.
        var graph = members.Select(m => m.Where(kv => kv.Key != "@context").ToDictionary(kv => kv.Key, kv => kv.Value)).ToList();
        return new Dictionary<string, object?>
        {
            ["@context"] = Context,
            ["@graph"] = graph,
            ["license"] = Constants.LicenseIri
        };
    }
}

/// <summary>Offset-paging parse + next-link helpers for catalog list endpoints.</summary>
internal static class CatalogPaging
{
    public static (int Page, int PageSize) Parse(HttpRequest request)
    {
        var page = int.TryParse(request.Query["page"], out var p) && p > 0 ? p : 1;
        var size = int.TryParse(request.Query["pageSize"], out var s)
            ? Math.Clamp(s, 1, Constants.MaxPageSize)
            : Constants.DefaultPageSize;
        return (page, size);
    }

    public static string? NextLink(string baseIri, int page, int pageSize, long total, string extraQuery)
    {
        if ((long)page * pageSize >= total)
            return null;
        var prefix = string.IsNullOrEmpty(extraQuery) ? string.Empty : extraQuery + "&";
        return $"{baseIri}?{prefix}page={page + 1}&pageSize={pageSize}";
    }

    /// <summary>Re-renders the query string excluding the given keys (e.g. paging params).</summary>
    public static string QueryExcept(HttpRequest request, params string[] exclude)
    {
        var skip = new HashSet<string>(exclude, StringComparer.OrdinalIgnoreCase);
        var pairs = request.Query
            .Where(kv => !skip.Contains(kv.Key))
            .SelectMany(kv => kv.Value.Select(v =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(v ?? string.Empty)}"));
        return string.Join('&', pairs);
    }
}
