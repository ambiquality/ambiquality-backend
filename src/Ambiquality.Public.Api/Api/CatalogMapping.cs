using Ambiquality.Core.Domain.Vocabulary;
using Ambiquality.Public.Api.Infrastructure.Catalog;

namespace Ambiquality.Public.Api.Api;

/// <summary>Maps raw evidence rows to public contracts (masking + IRIs + QUDT).</summary>
internal static class CatalogMappers
{
    public static BuildingResponse ToResponse(BuildingRow b, IriBuilder iri)
    {
        var (lat, lon) = CoordinateMasking.Apply(b.Latitude, b.Longitude, b.Anonymization);
        var address = b.Anonymization switch
        {
            "municipality" => new AddressDto(null,     b.City, null,       b.Country),
            "street"       => new AddressDto(b.Street, b.City, null,       b.Country),
            _              => new AddressDto(b.Street, b.City, b.Postcode, b.Country)
        };
        return new BuildingResponse(
            b.Id, iri.Building(b.Id), b.Name,
            address, b.BuildingTypeCode, lat, lon, b.YearBuilt, b.YearRenovated, Constants.LicenseIri);
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

    public static IReadOnlyDictionary<string, object?> ToBuilding(BuildingResponse b, IriBuilder iri) => new Dictionary<string, object?>
    {
        ["@context"] = Context,
        ["@id"] = b.Iri,
        ["@type"] = "ambiq:Building",
        ["ambiq:name"] = b.Name,
        ["ambiq:street"] = b.Address.Street,
        ["ambiq:city"] = b.Address.City,
        ["ambiq:postcode"] = b.Address.Postcode,
        ["ambiq:country"] = b.Address.Country,
        ["ambiq:buildingType"] = Concept(iri, Codelists.BuildingType, b.BuildingTypeCode),
        ["ambiq:latitude"] = b.Latitude,
        ["ambiq:longitude"] = b.Longitude,
        ["ambiq:yearBuilt"] = b.YearBuilt,
        ["ambiq:yearRenovated"] = b.YearRenovated,
        ["license"] = b.License
    };

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
