using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// Plain-JSON projection of one observable property — the substance-specific
/// concept a measurement's <c>sosa:observedProperty</c> points at. Carries the
/// QUDT dimensional kind + applicable unit and, where one exists, a link to the
/// authoritative external concept (EEA/EIONET air-quality pollutant).
/// </summary>
public sealed record PropertyResponse(
    string Code,
    string Iri,
    string Label,
    string? QuantityKindUri,
    string? UnitUri,
    string? ExactMatchIri,
    string? CloseMatchIri,
    string License)
{
    public static PropertyResponse From(ObservablePropertyVocabulary.Entry entry, IriBuilder iri)
    {
        var qudt = entry.Qudt;

        string? exact = null, close = null;
        if (entry.ExternalMatch is { } match)
        {
            if (match.IsExact) exact = match.Iri;
            else close = match.Iri;
        }

        return new PropertyResponse(
            entry.Code,
            iri.Property(entry.Code),
            entry.Label,
            qudt?.QuantityKindUri,
            qudt?.UnitUri,
            exact,
            close,
            Constants.LicenseIri);
    }
}

/// <summary>A page of plain-JSON observable properties (the vocabulary index).</summary>
public sealed record PropertyCollection(IReadOnlyList<PropertyResponse> Items, string License);

/// <summary>
/// Projects observable properties into JSON-LD as SOSA observable-properties that
/// are also SKOS concepts. A single resource carries an inline prefix
/// <c>@context</c>; collection members do not (the wrapping <c>@graph</c> carries it once).
/// </summary>
public static class PropertyJsonLd
{
    private static IReadOnlyDictionary<string, object?> Context() => new Dictionary<string, object?>
    {
        ["sosa"] = Constants.SosaNamespace,
        ["qudt"] = Constants.QudtSchemaNamespace,
        ["unit"] = Constants.QudtUnitBase,
        ["quantitykind"] = Constants.QudtQuantityKindBase,
        ["skos"] = Constants.SkosNamespace,
        ["rdfs"] = Constants.RdfsNamespace,
        ["dcterms"] = Constants.DctermsNamespace
    };

    private static Dictionary<string, object?> Id(string iri) => new() { ["@id"] = iri };

    public static IReadOnlyDictionary<string, object?> ToResource(PropertyResponse p, bool includeContext)
    {
        var doc = new Dictionary<string, object?>();

        if (includeContext)
            doc["@context"] = Context();

        doc["@id"] = p.Iri;
        doc["@type"] = new[] { "sosa:ObservableProperty", "skos:Concept" };
        doc["skos:notation"] = p.Code;
        doc["skos:prefLabel"] = p.Label;
        doc["rdfs:label"] = p.Label;

        if (p.QuantityKindUri is not null)
            doc["qudt:hasQuantityKind"] = Id(p.QuantityKindUri);

        if (p.UnitUri is not null)
            doc["qudt:applicableUnit"] = Id(p.UnitUri);

        if (p.ExactMatchIri is not null)
            doc["skos:exactMatch"] = Id(p.ExactMatchIri);

        if (p.CloseMatchIri is not null)
            doc["skos:closeMatch"] = Id(p.CloseMatchIri);

        doc["dcterms:license"] = p.License;

        return doc;
    }

    /// <summary>Wraps the whole vocabulary in a JSON-LD <c>@graph</c> with a single <c>@context</c>.</summary>
    public static IReadOnlyDictionary<string, object?> ToGraph(IEnumerable<PropertyResponse> items) =>
        new Dictionary<string, object?>
        {
            ["@context"] = Context(),
            ["@graph"] = items.Select(p => ToResource(p, includeContext: false)).ToList(),
            ["dcterms:license"] = Constants.LicenseIri
        };
}
