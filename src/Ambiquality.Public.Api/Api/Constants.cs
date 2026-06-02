namespace Ambiquality.Public.Api.Api;

/// <summary>
/// Cross-cutting constants for the public open-data API: licensing, pagination
/// bounds, cache lifetimes, vocabulary namespace IRIs and supported media types.
/// Defined once so every endpoint and contract stays consistent.
/// </summary>
public static class Constants
{
    /// <summary>CC BY 4.0 — emitted on every JSON/JSON-LD response body and as a Link header.</summary>
    public const string LicenseIri = "https://creativecommons.org/licenses/by/4.0/";

    public const string ApiVersion = "v1";

    /// <summary>Default page size for both keyset (observations) and offset (catalog) paging.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>Upper bound; larger client-requested sizes are clamped, not rejected.</summary>
    public const int MaxPageSize = 200;

    /// <summary>Cache lifetime (seconds) for list/detail responses — serves the read NFR.</summary>
    public const int CacheSeconds = 300;

    /// <summary>Cache lifetime for the rarely-changing JSON-LD context document.</summary>
    public const int ContextCacheSeconds = 86400;

    // Vocabulary namespace IRIs used when shaping JSON-LD.
    public const string AmbiqNamespace = "https://data.ambiquality.org/ns#";
    public const string SosaNamespace = "http://www.w3.org/ns/sosa/";
    public const string SsnNamespace = "http://www.w3.org/ns/ssn/";
    public const string QudtSchemaNamespace = "http://qudt.org/schema/qudt/";
    public const string QudtUnitBase = "http://qudt.org/vocab/unit/";
    public const string QudtQuantityKindBase = "http://qudt.org/vocab/quantitykind/";
    public const string SkosNamespace = "http://www.w3.org/2004/02/skos/core#";
    public const string RdfsNamespace = "http://www.w3.org/2000/01/rdf-schema#";
    public const string DctermsNamespace = "http://purl.org/dc/terms/";

    // Supported representation media types (content negotiation).
    public const string MediaTypeJson = "application/json";
    public const string MediaTypeJsonLd = "application/ld+json";
    public const string MediaTypeCsv = "text/csv";

    /// <summary>Pinned DCAT-AP 3.0.0 JSON-LD context IRI used by the catalog endpoint.</summary>
    public const string DcatApContextIri =
        "https://semiceu.github.io/DCAT-AP/releases/3.0.0/context/dcat-ap.jsonld";
}
