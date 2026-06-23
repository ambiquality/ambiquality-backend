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

    // Canonical MIME types — used for content negotiation (Accept matching) and as
    // dcat:mediaType values in DCAT-AP metadata. No parameters; bare type only.
    public const string MediaTypeJson = "application/json";
    public const string MediaTypeJsonLd = "application/ld+json";
    public const string MediaTypeCsv = "text/csv";

    // Wire Content-Type values for HTTP response headers — always declare encoding and,
    // for CSV, the header-row presence so CSVW processors and HTTP clients know the layout.
    public const string ContentTypeJson = MediaTypeJson + "; charset=utf-8";
    public const string ContentTypeJsonLd = MediaTypeJsonLd + "; charset=utf-8";
    public const string ContentTypeCsv = MediaTypeCsv + "; charset=utf-8; header=present";

    /// <summary>Pinned DCAT-AP 3.0.0 JSON-LD context IRI used by the catalog endpoint.</summary>
    public const string DcatApContextIri =
        "https://semiceu.github.io/DCAT-AP/releases/3.0.0/context/dcat-ap.jsonld";

    // EU Publications Office controlled-vocabulary IRIs (DCAT-AP / DCAT-AP-CZ expect
    // codelist values, not free strings). Concept IRIs use the /resource/authority/ form.
    /// <summary>Environment theme from the EU data-theme codelist (dcat:theme).</summary>
    public const string ThemeEnvironment =
        "http://publications.europa.eu/resource/authority/data-theme/ENVI";

    /// <summary>"Continuous" from the EU frequency codelist (dcterms:accrualPeriodicity);
    /// measurements stream in continuously.</summary>
    public const string FrequencyContinuous =
        "http://publications.europa.eu/resource/authority/frequency/CONT";

    /// <summary>JSON-LD file type from the EU file-type codelist (dcterms:format).</summary>
    public const string FileTypeJsonLd =
        "http://publications.europa.eu/resource/authority/file-type/JSON_LD";

    /// <summary>CSV file type from the EU file-type codelist (dcterms:format).</summary>
    public const string FileTypeCsv =
        "http://publications.europa.eu/resource/authority/file-type/CSV";
}
