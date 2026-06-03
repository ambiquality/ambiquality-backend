using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Public.Api.Api;

/// <summary>Plain-JSON projection of one codelist concept (a SKOS <c>skos:Concept</c>).</summary>
public sealed record CodelistConceptResponse(
    string Code,
    string Iri,
    string Scheme,
    string SchemeIri,
    string LabelEn,
    string LabelCs,
    string License)
{
    public static CodelistConceptResponse From(Codelist codelist, CodelistConcept concept, IriBuilder iri) => new(
        concept.Code,
        iri.CodelistConcept(codelist.Scheme, concept.Code),
        codelist.Scheme,
        iri.CodelistScheme(codelist.Scheme),
        concept.LabelEn,
        concept.LabelCs,
        Constants.LicenseIri);
}

/// <summary>Plain-JSON projection of a whole codelist (a SKOS <c>skos:ConceptScheme</c>).</summary>
public sealed record CodelistSchemeResponse(
    string Scheme,
    string Iri,
    IReadOnlyList<CodelistConceptResponse> Concepts,
    string License)
{
    public static CodelistSchemeResponse From(Codelist codelist, IriBuilder iri) => new(
        codelist.Scheme,
        iri.CodelistScheme(codelist.Scheme),
        codelist.Concepts.Select(c => CodelistConceptResponse.From(codelist, c, iri)).ToList(),
        Constants.LicenseIri);
}

/// <summary>The codelist index: one reference per published scheme.</summary>
public sealed record CodelistIndexResponse(IReadOnlyList<CodelistSchemeRef> Schemes, string License);

/// <summary>A scheme reference in the codelist index.</summary>
public sealed record CodelistSchemeRef(string Scheme, string Iri);

/// <summary>
/// Projects codelists into JSON-LD as SKOS concept schemes and concepts. A single
/// resource carries an inline prefix <c>@context</c>; collection members do not
/// (the wrapping <c>@graph</c> object carries it once). prefLabels are language-tagged
/// (cs + en) so the codelists serve as bilingual <em>číselníky</em>.
/// </summary>
public static class CodelistJsonLd
{
    private static IReadOnlyDictionary<string, object?> Context() => new Dictionary<string, object?>
    {
        ["skos"] = Constants.SkosNamespace,
        ["rdfs"] = Constants.RdfsNamespace,
        ["dcterms"] = Constants.DctermsNamespace
    };

    private static Dictionary<string, object?> Id(string iri) => new() { ["@id"] = iri };

    private static object[] PrefLabel(CodelistConceptResponse c) =>
    [
        new Dictionary<string, object?> { ["@language"] = "en", ["@value"] = c.LabelEn },
        new Dictionary<string, object?> { ["@language"] = "cs", ["@value"] = c.LabelCs }
    ];

    public static IReadOnlyDictionary<string, object?> ToConcept(CodelistConceptResponse c, bool includeContext)
    {
        var doc = new Dictionary<string, object?>();

        if (includeContext)
            doc["@context"] = Context();

        doc["@id"] = c.Iri;
        doc["@type"] = "skos:Concept";
        doc["skos:notation"] = c.Code;
        doc["skos:prefLabel"] = PrefLabel(c);
        doc["skos:inScheme"] = Id(c.SchemeIri);
        doc["dcterms:license"] = c.License;

        return doc;
    }

    public static IReadOnlyDictionary<string, object?> ToScheme(CodelistSchemeResponse s, bool includeContext)
    {
        var doc = new Dictionary<string, object?>();

        if (includeContext)
            doc["@context"] = Context();

        doc["@id"] = s.Iri;
        doc["@type"] = "skos:ConceptScheme";
        // Embed the members so the scheme document is self-contained and dereferenceable.
        doc["skos:hasTopConcept"] = s.Concepts.Select(c => Id(c.Iri)).ToList();
        doc["skos:member"] = s.Concepts.Select(c => ToConcept(c, includeContext: false)).ToList();
        doc["dcterms:license"] = s.License;

        return doc;
    }

    /// <summary>Wraps every scheme in a JSON-LD <c>@graph</c> with a single <c>@context</c> (the index).</summary>
    public static IReadOnlyDictionary<string, object?> ToGraph(IEnumerable<CodelistSchemeResponse> schemes) =>
        new Dictionary<string, object?>
        {
            ["@context"] = Context(),
            ["@graph"] = schemes.Select(s => ToScheme(s, includeContext: false)).ToList(),
            ["dcterms:license"] = Constants.LicenseIri
        };
}
