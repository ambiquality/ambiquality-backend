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
            .WithDescription("Open-data catalog record describing the IEQ dataset (JSON-LD / JSON).");
    }

    private static async Task<IResult> GetCatalog(
        HttpContext http, IeqDbContext db, IEvidenceCatalog catalog, IExportCatalog exports,
        IConfiguration config, CancellationToken ct)
    {
        // DCAT is RDF, so JSON-LD is the default; only an explicit application/json
        // downgrades to plain JSON. text/csv is not meaningful here.
        if (!ContentNegotiation.TryResolveFormat(http.Request, out var format) || format == ResponseFormat.Csv)
            return Problems.UnsupportedMediaType();

        var iri = IriBuilder.ForRequest(http.Request, config);
        var (start, end) = await CatalogMetadataQuery.GetTemporalExtentAsync(db, ct);
        var extent = await catalog.GetSpatialExtentAsync(ct);
        var exportRows = await exports.GetExportsAsync(ct);

        var document = BuildDocument(iri, start, end, extent, exportRows);
        http.Response.Headers.CacheControl = $"public, max-age={Constants.CacheSeconds}";

        var contentType = format == ResponseFormat.Json ? Constants.MediaTypeJson : Constants.MediaTypeJsonLd;
        return Results.Json(document, contentType: contentType);
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
            ["dcterms:title"] = DatasetTitle,
            ["dcterms:description"] =
                "Indoor Environmental Quality (IEQ) sensor measurements — CO₂, temperature, humidity, "
                + "particulate matter, VOCs, acoustics and light — published as open linked data.",
            ["dcterms:publisher"] = new Dictionary<string, object?>
            {
                ["@type"] = "foaf:Agent",
                ["foaf:name"] = PublisherName
            },
            ["dcterms:license"] = new Dictionary<string, object?> { ["@id"] = Constants.LicenseIri },
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
            ["dcterms:title"] = DatasetTitle,
            ["dcterms:license"] = new Dictionary<string, object?> { ["@id"] = Constants.LicenseIri },
            ["dcat:dataset"] = dataset
        };
    }

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

        if (e.FileSizeBytes is { } size)
            dist["dcat:byteSize"] = size;

        // Zipped CSV archives share the live CSV's CSVW tabular schema.
        if (string.Equals(e.MediaType, Constants.MediaTypeCsv, StringComparison.Ordinal))
            dist["dcterms:conformsTo"] = new Dictionary<string, object?> { ["@id"] = csvSchema };

        return dist;
    }

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
