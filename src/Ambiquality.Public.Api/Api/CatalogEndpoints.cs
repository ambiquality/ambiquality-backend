using System.Globalization;
using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Public.Api.Application.Catalog;
using Ambiquality.Public.Api.Infrastructure.Catalog;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// DCAT-AP 3.0 catalog metadata (F16). One <c>dcat:Dataset</c> describes the whole
/// platform with two distributions (JSON-LD and CSV), spatial + temporal coverage
/// derived from live data, a contact point, and the CC BY 4.0 license. Served as
/// JSON-LD by default; plain JSON on explicit <c>Accept: application/json</c>.
/// </summary>
public static class CatalogEndpoints
{
    private const string ContactEmail = "info@ambiquality.org";
    private const string DatasetTitle = "Ambiquality IEQ Open Data";
    private const string PublisherName = "Vilém Charwot, VŠE Prague";

    // DCAT-AP-CZ requires cs+en language-tagged title/description on the Catalog and Dataset.
    private static readonly (string Lang, string Value)[] CatalogTitle =
    [
        ("en", "Ambiquality IEQ Open Data Catalogue"),
        ("cs", "Ambiquality – katalog otevřených dat o kvalitě vnitřního prostředí")
    ];

    private static readonly (string Lang, string Value)[] CatalogDescription =
    [
        ("en", "Open-data catalogue of the Ambiquality platform, publishing indoor "
             + "environmental quality (IEQ) sensor measurements as linked open data. Coverage "
             + "spans the four IEQ domains: indoor air quality, thermal comfort, acoustic "
             + "comfort and visual comfort."),
        ("cs", "Katalog otevřených dat platformy Ambiquality zveřejňující měření kvality "
             + "vnitřního prostředí (IEQ) jako propojená otevřená data. Pokrytí zahrnuje "
             + "čtyři oblasti IEQ: kvalitu vnitřního vzduchu, tepelnou pohodu, akustickou "
             + "pohodu a zrakovou pohodu.")
    ];

    private static readonly (string Lang, string Value)[] DatasetTitleMultilingual =
    [
        ("en", DatasetTitle),
        ("cs", "Ambiquality – otevřená data o kvalitě vnitřního prostředí")
    ];

    private static readonly (string Lang, string Value)[] DatasetDescription =
    [
        ("en", "Indoor Environmental Quality (IEQ) sensor measurements across four domains: "
             + "indoor air quality (CO₂, VOCs, particulate matter, relative humidity), thermal "
             + "comfort (air temperature, relative humidity), acoustic comfort (sound pressure "
             + "level) and visual comfort (illuminance) — published as open linked data."),
        ("cs", "Měření kvality vnitřního prostředí (IEQ) ve čtyřech oblastech: kvalita "
             + "vnitřního vzduchu (CO₂, VOC, prachové částice, relativní vlhkost), tepelná "
             + "pohoda (teplota vzduchu, relativní vlhkost), akustická pohoda (hladina "
             + "akustického tlaku) a zraková pohoda (osvětlenost) — zveřejněná jako propojená "
             + "otevřená data.")
    ];

    private static readonly (string Lang, string Value)[] Keywords =
    [
        ("en", "indoor environmental quality"), ("cs", "kvalita vnitřního prostředí"),
        ("en", "IEQ"),
        ("en", "indoor air quality"), ("cs", "kvalita vnitřního vzduchu"),
        ("en", "thermal comfort"), ("cs", "tepelná pohoda"),
        ("en", "acoustic comfort"), ("cs", "akustická pohoda"),
        ("en", "visual comfort"), ("cs", "zraková pohoda"),
        ("en", "CO₂"),
        ("en", "particulate matter"),
        ("en", "open data"), ("cs", "otevřená data")
    ];

    private static readonly object[] DcatContext =
    [
        Constants.DcatApContextIri,
        new Dictionary<string, object?>
        {
            ["ambiq"] = Constants.AmbiqNamespace,
            ["vcard"] = "http://www.w3.org/2006/vcard/ns#",
            ["foaf"] = "http://xmlns.com/foaf/0.1/",
            ["geosparql"] = "http://www.opengis.net/ont/geosparql#",
            ["xsd"] = "http://www.w3.org/2001/XMLSchema#"
        }
    ];

    public static void MapDcatCatalogEndpoints(this WebApplication app)
    {
        app.MapMethods($"/{Constants.ApiVersion}/catalog", ["GET", "HEAD"], GetCatalog)
            .WithTags("Catalog")
            .WithName("GetDcatCatalog")
            .WithSummary("DCAT-AP 3.0 catalog metadata")
            .WithDescription("Open-data catalog record describing the IEQ dataset. Served as application/ld+json by default; application/json is also accepted (same document). text/csv is not supported (406).")
            .Produces(StatusCodes.Status200OK, contentType: Constants.MediaTypeJsonLd)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);
    }

    private static async Task<IResult> GetCatalog(
        HttpContext http, IeqDbContext db, IEvidenceCatalog catalog, IExportCatalog exports,
        IConfiguration config, CancellationToken ct)
    {
        // The catalog is a JSON-LD-only resource (DCAT-AP is RDF). application/json is
        // treated as an alias for JSON-LD (same document, same content-type); text/csv is rejected.
        if (!ContentNegotiation.TryResolveJsonLdOnly(http.Request))
            return Problems.UnsupportedMediaType();

        var iri = IriBuilder.ForRequest(http.Request, config);
        var (start, end) = await CatalogMetadataQuery.GetTemporalExtentAsync(db, ct);
        var extent = await catalog.GetSpatialExtentAsync(ct);
        var exportRows = await exports.GetExportsAsync(ct);

        var document = BuildDocument(iri, start, end, extent, exportRows);
        http.Response.Headers.CacheControl = $"public, max-age={Constants.CacheSeconds}";

        return Results.Json(document, contentType: Constants.ContentTypeJsonLd);
    }

    private static IReadOnlyDictionary<string, object?> BuildDocument(
        IriBuilder iri, DateTime? start, DateTime? end, SpatialExtent? extent,
        IReadOnlyList<ExportDistributionRow> exports)
    {
        // Live API access points plus one downloadable archive per published export.
        // CSV distributions advertise the CSVW tabular schema via dcterms:conformsTo.
        var csvSchema = iri.CsvMetadata();
        var distributions = new List<object>
        {
            Distribution(iri.Observations(), Constants.MediaTypeJsonLd, "Observations as JSON-LD"),
            Distribution(iri.ObservationsCsv(), Constants.MediaTypeCsv, "Observations as CSV", conformsTo: csvSchema)
        };
        distributions.AddRange(exports.Select(e => DownloadDistribution(e, csvSchema)));

        var dataset = new Dictionary<string, object?>
        {
            ["@id"] = $"{iri.Catalog()}#dataset",
            ["@type"] = "dcat:Dataset",
            ["dcterms:title"] = Multilingual(DatasetTitleMultilingual),
            ["dcterms:description"] = Multilingual(DatasetDescription),
            ["dcterms:publisher"] = Publisher(),
            ["dcterms:license"] = new Dictionary<string, object?> { ["@id"] = Constants.LicenseIri },
            ["dcat:theme"] = new Dictionary<string, object?> { ["@id"] = Constants.ThemeEnvironment },
            ["dcat:keyword"] = Multilingual(Keywords),
            // Measurements stream in continuously; the dataset is updated continuously.
            ["dcterms:accrualPeriodicity"] = new Dictionary<string, object?> { ["@id"] = Constants.FrequencyContinuous },
            ["dcat:contactPoint"] = new Dictionary<string, object?>
            {
                ["@type"] = "vcard:Individual",
                ["vcard:fn"] = PublisherName,
                ["vcard:hasEmail"] = new Dictionary<string, object?> { ["@id"] = $"mailto:{ContactEmail}" }
            },
            ["dcat:distribution"] = distributions
        };

        if (start is not null)
            dataset["dcterms:issued"] = TypedDate(start.Value, "xsd:date");

        if (start is not null && end is not null)
            dataset["dcterms:temporal"] = new Dictionary<string, object?>
            {
                ["@type"] = "dcterms:PeriodOfTime",
                ["dcat:startDate"] = TypedDate(start.Value, "xsd:dateTime"),
                ["dcat:endDate"] = TypedDate(end.Value, "xsd:dateTime")
            };

        if (extent is not null)
            dataset["dcterms:spatial"] = new Dictionary<string, object?>
            {
                ["@type"] = "dcterms:Location",
                ["dcat:bbox"] = new Dictionary<string, object?>
                {
                    ["@type"] = "geosparql:wktLiteral",
                    ["@value"] = WktEnvelope(extent)
                }
            };

        return new Dictionary<string, object?>
        {
            ["@context"] = DcatContext,
            ["@id"] = iri.Catalog(),
            ["@type"] = "dcat:Catalog",
            ["dcterms:title"] = Multilingual(CatalogTitle),
            // dcterms:description is DCAT-AP-CZ-mandatory on the Catalog; dcterms:publisher is
            // mandatory in base DCAT-AP 3.0. Both were previously absent at the Catalog level.
            ["dcterms:description"] = Multilingual(CatalogDescription),
            ["dcterms:publisher"] = Publisher(),
            ["dcterms:license"] = new Dictionary<string, object?> { ["@id"] = Constants.LicenseIri },
            ["dcat:dataset"] = dataset
        };
    }

    // Language-tagged literals ({"@language","@value"} form) — DCAT-AP-CZ wants parallel
    // cs+en versions of title/description/keyword, not bare strings.
    private static object[] Multilingual((string Lang, string Value)[] values) =>
        values.Select(object (v) => new Dictionary<string, object?>
        {
            ["@language"] = v.Lang,
            ["@value"] = v.Value
        }).ToArray();

    // Publisher as a free-text foaf:Agent. DCAT-AP-CZ ultimately wants dcterms:publisher to be an
    // IRI from the Czech OVM/RPP register (orgán veřejné moci). This project is authored by an
    // individual student, NOT an OVM — confirmed by e-mail with the national open-data coordinator —
    // so no such IRI can be minted and full DCAT-AP-CZ conformance is structurally impossible.
    // The catalogue is therefore DCAT-AP-CZ-aligned only in part; see Public.Api/README.md.
    private static Dictionary<string, object?> Publisher() => new()
    {
        ["@type"] = "foaf:Agent",
        ["foaf:name"] = PublisherName
    };

    private static Dictionary<string, object?> Distribution(
        string accessUrl, string mediaType, string title, string? conformsTo = null)
    {
        var dist = new Dictionary<string, object?>
        {
            ["@type"] = "dcat:Distribution",
            ["dcterms:title"] = title,
            ["dcat:accessURL"] = new Dictionary<string, object?> { ["@id"] = accessUrl },
            ["dcat:mediaType"] = mediaType,
            ["dcterms:license"] = new Dictionary<string, object?> { ["@id"] = Constants.LicenseIri }
        };
        if (FileTypeFor(mediaType) is { } formatIri)
            dist["dcterms:format"] = new Dictionary<string, object?> { ["@id"] = formatIri };
        if (conformsTo is not null)
            dist["dcterms:conformsTo"] = new Dictionary<string, object?> { ["@id"] = conformsTo };
        return dist;
    }

    private static Dictionary<string, object?> DownloadDistribution(ExportDistributionRow e, string csvSchema)
    {
        var monthStart = new DateTime(e.Year, e.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var dist = new Dictionary<string, object?>
        {
            ["@type"] = "dcat:Distribution",
            ["dcterms:title"] = $"Measurements {e.Year:D4}-{e.Month:D2} ({e.MediaType}, zipped)",
            ["dcat:downloadURL"] = new Dictionary<string, object?> { ["@id"] = e.DownloadUrl },
            ["dcat:mediaType"] = e.MediaType,
            ["dcat:compressFormat"] = e.CompressFormat,
            ["dcterms:license"] = new Dictionary<string, object?> { ["@id"] = Constants.LicenseIri },
            ["dcterms:temporal"] = new Dictionary<string, object?>
            {
                ["@type"] = "dcterms:PeriodOfTime",
                ["dcat:startDate"] = TypedDate(monthStart, "xsd:dateTime"),
                ["dcat:endDate"] = TypedDate(monthEnd, "xsd:dateTime")
            }
        };

        if (FileTypeFor(e.MediaType) is { } formatIri)
            dist["dcterms:format"] = new Dictionary<string, object?> { ["@id"] = formatIri };

        if (e.FileSizeBytes is { } size)
            dist["dcat:byteSize"] = size;

        // Zipped CSV archives share the live CSV's CSVW tabular schema.
        if (string.Equals(e.MediaType, Constants.MediaTypeCsv, StringComparison.Ordinal))
            dist["dcterms:conformsTo"] = new Dictionary<string, object?> { ["@id"] = csvSchema };

        return dist;
    }

    // Map a media type to its EU file-type codelist IRI (dcterms:format), alongside the
    // existing dcat:mediaType. null for an unmapped type — the property is simply omitted.
    private static string? FileTypeFor(string mediaType) => mediaType switch
    {
        Constants.MediaTypeJsonLd => Constants.FileTypeJsonLd,
        Constants.MediaTypeCsv => Constants.FileTypeCsv,
        _ => null
    };

    private static Dictionary<string, object?> TypedDate(DateTime value, string xsdType) => new()
    {
        ["@type"] = xsdType,
        ["@value"] = xsdType == "xsd:date"
            ? value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : value.ToString("O", CultureInfo.InvariantCulture)
    };

    private static string WktEnvelope(SpatialExtent e) =>
        string.Format(CultureInfo.InvariantCulture,
            "POLYGON(({0} {1}, {2} {1}, {2} {3}, {0} {3}, {0} {1}))",
            e.MinLon, e.MinLat, e.MaxLon, e.MaxLat);
}
