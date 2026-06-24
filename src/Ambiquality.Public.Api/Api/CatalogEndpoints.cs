using System.Globalization;
using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Public.Api.Application.Catalog;
using Ambiquality.Public.Api.Infrastructure.Catalog;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// DCAT-AP 3.0 catalog metadata (F16/F17). The catalog publishes two things:
/// a continuous live <c>dcat:Dataset</c> (the queryable API, JSON-LD + CSV access
/// points, spatial + temporal coverage from live data) and — when monthly archives
/// exist — a <c>dcat:DatasetSeries</c> whose members are one <c>dcat:Dataset</c> per
/// calendar month. A monthly slice is distinct data, so it is a member dataset with
/// its own gzip distributions, never another distribution of the live dataset. Served
/// as JSON-LD by default; plain JSON on explicit <c>Accept: application/json</c>.
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

    private static readonly (string Lang, string Value)[] SeriesTitle =
    [
        ("en", "Ambiquality IEQ measurements — monthly archives"),
        ("cs", "Ambiquality – měření IEQ, měsíční archivy")
    ];

    private static readonly (string Lang, string Value)[] SeriesDescription =
    [
        ("en", "Downloadable monthly archives of the Ambiquality IEQ measurements, one "
             + "dataset per calendar month. Each month is published as a single "
             + "gzip-compressed CSV file and a single gzip-compressed JSON-LD file."),
        ("cs", "Měsíční archivy měření IEQ platformy Ambiquality ke stažení, jedna datová "
             + "sada na kalendářní měsíc. Každý měsíc je publikován jako jeden gzipem "
             + "komprimovaný CSV soubor a jeden gzipem komprimovaný JSON-LD soubor.")
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
        // The catalog always carries the continuous live dataset. When monthly archives
        // exist, it additionally carries a dcat:DatasetSeries and one member dcat:Dataset
        // per month — newest first.
        var datasets = new List<object> { LiveDataset(iri, start, end, extent) };

        var months = exports
            .GroupBy(e => (e.Year, e.Month))
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .ToList();

        if (months.Count > 0)
        {
            datasets.Add(MonthlySeries(iri, months));
            datasets.AddRange(months.Select(object (m) => MonthlyDataset(iri, m)));
        }

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
            ["dcterms:license"] = Ref(Constants.LicenseIri),
            ["dcat:dataset"] = datasets
        };
    }

    /// <summary>
    /// The continuous live dataset: the queryable API exposed as two distributions
    /// (JSON-LD + CSV access points), updated continuously. The CSV distribution
    /// advertises the CSVW tabular schema via dcterms:conformsTo.
    /// </summary>
    private static Dictionary<string, object?> LiveDataset(
        IriBuilder iri, DateTime? start, DateTime? end, SpatialExtent? extent)
    {
        var csvSchema = iri.CsvMetadata();
        var dataset = new Dictionary<string, object?>
        {
            ["@id"] = $"{iri.Catalog()}#dataset",
            ["@type"] = "dcat:Dataset",
            ["dcterms:title"] = Multilingual(DatasetTitleMultilingual),
            ["dcterms:description"] = Multilingual(DatasetDescription),
            ["dcterms:publisher"] = Publisher(),
            ["dcterms:license"] = Ref(Constants.LicenseIri),
            ["dcat:theme"] = Ref(Constants.ThemeEnvironment),
            ["dcat:keyword"] = Multilingual(Keywords),
            // Measurements stream in continuously; the dataset is updated continuously.
            ["dcterms:accrualPeriodicity"] = Ref(Constants.FrequencyContinuous),
            ["dcat:contactPoint"] = ContactPoint(),
            ["dcat:distribution"] = new List<object>
            {
                Distribution(iri.Observations(), Constants.MediaTypeJsonLd, "Observations as JSON-LD"),
                Distribution(iri.ObservationsCsv(), Constants.MediaTypeCsv, "Observations as CSV", conformsTo: csvSchema)
            }
        };

        if (start is not null)
            dataset["dcterms:issued"] = TypedDate(start.Value, "xsd:date");

        if (start is not null && end is not null)
            dataset["dcterms:temporal"] = Period(start.Value, end.Value);

        if (extent is not null)
            dataset["dcterms:spatial"] = Spatial(extent);

        return dataset;
    }

    /// <summary>
    /// The dcat:DatasetSeries grouping the monthly archive members. Its temporal extent
    /// spans the published months and dcat:first/dcat:last point at the chronological
    /// ends; <paramref name="months"/> is ordered newest-first.
    /// </summary>
    private static Dictionary<string, object?> MonthlySeries(
        IriBuilder iri, IReadOnlyList<IGrouping<(short Year, short Month), ExportDistributionRow>> months)
    {
        var newest = months[0].Key;
        var oldest = months[^1].Key;

        return new Dictionary<string, object?>
        {
            ["@id"] = SeriesIri(iri),
            ["@type"] = "dcat:DatasetSeries",
            ["dcterms:title"] = Multilingual(SeriesTitle),
            ["dcterms:description"] = Multilingual(SeriesDescription),
            ["dcterms:publisher"] = Publisher(),
            ["dcterms:license"] = Ref(Constants.LicenseIri),
            ["dcat:theme"] = Ref(Constants.ThemeEnvironment),
            ["dcterms:accrualPeriodicity"] = Ref(Constants.FrequencyMonthly),
            ["dcterms:temporal"] = Period(MonthStart(oldest), MonthStart(newest).AddMonths(1)),
            ["dcat:first"] = Ref(MonthDatasetIri(iri, oldest)),
            ["dcat:last"] = Ref(MonthDatasetIri(iri, newest))
        };
    }

    /// <summary>
    /// One month's archive as a member dcat:Dataset: linked to the series via
    /// dcat:inSeries, bounded by dcterms:temporal to the calendar month, and carrying
    /// one gzip download distribution per published format.
    /// </summary>
    private static Dictionary<string, object?> MonthlyDataset(
        IriBuilder iri, IGrouping<(short Year, short Month), ExportDistributionRow> month)
    {
        var (year, mon) = month.Key;
        var monthStart = MonthStart(month.Key);
        var csvSchema = iri.CsvMetadata();

        return new Dictionary<string, object?>
        {
            ["@id"] = MonthDatasetIri(iri, month.Key),
            ["@type"] = "dcat:Dataset",
            ["dcterms:title"] = Multilingual(
            [
                ("en", $"Ambiquality IEQ measurements {year:D4}-{mon:D2}"),
                ("cs", $"Ambiquality – měření IEQ za {year:D4}-{mon:D2}")
            ]),
            ["dcterms:description"] = Multilingual(
            [
                ("en", $"Indoor environmental quality measurements for the {year:D4}-{mon:D2} "
                     + "calendar month, as a downloadable monthly archive."),
                ("cs", $"Měření kvality vnitřního prostředí za kalendářní měsíc {year:D4}-{mon:D2} "
                     + "ke stažení jako měsíční archiv.")
            ]),
            ["dcterms:publisher"] = Publisher(),
            ["dcterms:license"] = Ref(Constants.LicenseIri),
            ["dcat:theme"] = Ref(Constants.ThemeEnvironment),
            ["dcat:inSeries"] = Ref(SeriesIri(iri)),
            ["dcterms:temporal"] = Period(monthStart, monthStart.AddMonths(1)),
            ["dcat:distribution"] = month.Select(object (e) => DownloadDistribution(e, csvSchema)).ToList()
        };
    }

    private static string SeriesIri(IriBuilder iri) => $"{iri.Catalog()}#series";

    private static string MonthDatasetIri(IriBuilder iri, (short Year, short Month) m) =>
        $"{iri.Catalog()}#dataset-{m.Year:D4}-{m.Month:D2}";

    private static DateTime MonthStart((short Year, short Month) m) =>
        new(m.Year, m.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Dictionary<string, object?> Ref(string id) => new() { ["@id"] = id };

    private static Dictionary<string, object?> ContactPoint() => new()
    {
        ["@type"] = "vcard:Individual",
        ["vcard:fn"] = PublisherName,
        ["vcard:hasEmail"] = Ref($"mailto:{ContactEmail}")
    };

    private static Dictionary<string, object?> Period(DateTime start, DateTime end) => new()
    {
        ["@type"] = "dcterms:PeriodOfTime",
        ["dcat:startDate"] = TypedDate(start, "xsd:dateTime"),
        ["dcat:endDate"] = TypedDate(end, "xsd:dateTime")
    };

    private static Dictionary<string, object?> Spatial(SpatialExtent extent) => new()
    {
        ["@type"] = "dcterms:Location",
        ["dcat:bbox"] = new Dictionary<string, object?>
        {
            ["@type"] = "geosparql:wktLiteral",
            ["@value"] = WktEnvelope(extent)
        }
    };

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

    // One published monthly archive object: a single gzip-compressed file (dcat:compressFormat
    // = application/gzip), not a multi-file container. The owning month dataset carries the
    // temporal bound, so the distribution does not repeat it.
    private static Dictionary<string, object?> DownloadDistribution(ExportDistributionRow e, string csvSchema)
    {
        var dist = new Dictionary<string, object?>
        {
            ["@type"] = "dcat:Distribution",
            ["dcterms:title"] = $"Measurements {e.Year:D4}-{e.Month:D2} ({e.MediaType}, gzip)",
            ["dcat:downloadURL"] = Ref(e.DownloadUrl),
            ["dcat:mediaType"] = e.MediaType,
            ["dcat:compressFormat"] = e.CompressFormat,
            ["dcterms:license"] = Ref(Constants.LicenseIri)
        };

        if (FileTypeFor(e.MediaType) is { } formatIri)
            dist["dcterms:format"] = Ref(formatIri);

        if (e.FileSizeBytes is { } size)
            dist["dcat:byteSize"] = size;

        // Gzipped CSV archives share the live CSV's CSVW tabular schema.
        if (string.Equals(e.MediaType, Constants.MediaTypeCsv, StringComparison.Ordinal))
            dist["dcterms:conformsTo"] = Ref(csvSchema);

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
