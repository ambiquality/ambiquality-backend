using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;

namespace Ambiquality.Public.Api.Tests;

public sealed class DcatCatalogTests(TimescaleFixture fixture) : PublicApiTestBase(fixture)
{
    private const string LicenseIri = "https://creativecommons.org/licenses/by/4.0/";

    [Fact]
    public async Task Catalog_ContentType_IsJsonLd()
    {
        var response = await Client.GetAsync("/v1/catalog");
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/ld+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Catalog_WithJsonAccept_ReturnsJsonLd()
    {
        // application/json is accepted as an alias — clients that omit Accept or send the
        // generic JSON type must not be broken. The response is always application/ld+json.
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/catalog");
        request.Headers.Add("Accept", "application/json");
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/ld+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Catalog_WithWildcardAccept_ReturnsJsonLd()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/catalog");
        request.Headers.Add("Accept", "*/*");
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/ld+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Catalog_WithJsonAndWildcardAccept_ReturnsJsonLd()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/catalog");
        request.Headers.Add("Accept", "application/json, */*;q=0.9");
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/ld+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Catalog_WithCsvAccept_Returns406()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/catalog");
        request.Headers.Add("Accept", "text/csv");
        var response = await Client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.NotAcceptable, response.StatusCode);
    }

    [Fact]
    public async Task Catalog_Context_IsSelfContainedPrefixMap()
    {
        // Regression: the @context must NOT pull in the remote SEMIC DCAT-AP JSON-LD
        // context. That document defines colon-bearing terms ("Xsd:dateTime", …) whose
        // prefix is undefined, so strict JSON-LD processors reject the whole context
        // (INVALID_IRI_MAPPING) — breaking the EU ITB and lkod validators. The context
        // must be a single inline object that itself defines every prefix the body uses.
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var context = doc.GetProperty("@context");

        Assert.Equal(JsonValueKind.Object, context.ValueKind);
        Assert.DoesNotContain("semiceu.github.io", context.GetRawText());
        foreach (var prefix in new[] { "dcat", "dcterms", "foaf", "vcard", "geosparql", "xsd", "pu" })
            Assert.True(context.TryGetProperty(prefix, out _), $"missing prefix '{prefix}'");
    }

    [Fact]
    public async Task Catalog_IsDcatCatalogWithLiveDataset()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");

        Assert.Equal("dcat:Catalog", doc.GetProperty("@type").GetString());
        var dataset = LiveDataset(doc);
        Assert.Equal("dcat:Dataset", dataset.GetProperty("@type").GetString());
        Assert.Equal("Ambiquality IEQ Open Data", LangValue(dataset.GetProperty("dcterms:title"), "en"));
    }

    [Fact]
    public async Task Catalog_HasMandatoryCatalogLevelPublisherAndDescription()
    {
        // dcterms:publisher is mandatory in base DCAT-AP 3.0; dcterms:description is
        // DCAT-AP-CZ-mandatory. Both must appear on the Catalog node, not only the Dataset.
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");

        Assert.Equal("foaf:Agent", doc.GetProperty("dcterms:publisher").GetProperty("@type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            doc.GetProperty("dcterms:publisher").GetProperty("foaf:name").GetString()));

        // cs + en language-tagged title and description.
        Assert.NotNull(LangValue(doc.GetProperty("dcterms:title"), "cs"));
        Assert.NotNull(LangValue(doc.GetProperty("dcterms:title"), "en"));
        Assert.NotNull(LangValue(doc.GetProperty("dcterms:description"), "cs"));
        Assert.NotNull(LangValue(doc.GetProperty("dcterms:description"), "en"));
    }

    [Fact]
    public async Task Catalog_LiveDatasetHasThemeKeywordAndPeriodicityFromCodelists()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var dataset = LiveDataset(doc);

        Assert.EndsWith("/data-theme/ENVI", dataset.GetProperty("dcat:theme").GetProperty("@id").GetString());
        Assert.EndsWith("/frequency/CONT",
            dataset.GetProperty("dcterms:accrualPeriodicity").GetProperty("@id").GetString());

        // Keywords are language-tagged and include both cs and en entries.
        var keywords = dataset.GetProperty("dcat:keyword").EnumerateArray().ToList();
        Assert.Contains(keywords, k => k.GetProperty("@language").GetString() == "cs");
        Assert.Contains(keywords, k => k.GetProperty("@language").GetString() == "en");

        // cs + en language-tagged dataset description.
        Assert.NotNull(LangValue(dataset.GetProperty("dcterms:description"), "cs"));
        Assert.NotNull(LangValue(dataset.GetProperty("dcterms:description"), "en"));
    }

    [Fact]
    public async Task Catalog_AllDistributionsCarryFileTypeFormat()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");

        // Every distribution across the live dataset and all monthly members advertises
        // both dcat:mediaType and the EU file-type dcterms:format.
        var distributions = AllDistributions(doc);
        Assert.NotEmpty(distributions);
        Assert.All(distributions, d =>
            Assert.Contains("/file-type/", d.GetProperty("dcterms:format").GetProperty("@id").GetString()));
    }

    [Fact]
    public async Task Catalog_EveryDistributionCarriesTermsOfUse()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");

        // DCAT-AP-CZ requires a pu:specifikace (terms-of-use) node on every distribution; the
        // LKOD validator warns ("Chybí podmínky užití") when it is absent. Each spec must declare
        // the work, the database-as-work, the sui-generis right and the personal-data status.
        var distributions = AllDistributions(doc);
        Assert.NotEmpty(distributions);
        Assert.All(distributions, d =>
        {
            var spec = d.GetProperty("pu:specifikace");
            Assert.Equal("pu:Specifikace", spec.GetProperty("@type").GetString());
            Assert.Equal(LicenseIri, spec.GetProperty("pu:autorské-dílo").GetProperty("@id").GetString());
            Assert.Equal(LicenseIri, spec.GetProperty("pu:databáze-jako-autorské-dílo").GetProperty("@id").GetString());
            Assert.Contains("není-chráněna",
                spec.GetProperty("pu:databáze-chráněná-zvláštními-právy").GetProperty("@id").GetString());
            Assert.Contains("neobsahuje-osobní-údaje",
                spec.GetProperty("pu:osobní-údaje").GetProperty("@id").GetString());
            Assert.Equal("cs", spec.GetProperty("pu:autor").GetProperty("@language").GetString());
        });
    }

    [Fact]
    public async Task Catalog_LiveDatasetHasTwoDistributionsAndContactPoint()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var dataset = LiveDataset(doc);

        var distributions = dataset.GetProperty("dcat:distribution").EnumerateArray().ToList();
        Assert.Equal(2, distributions.Count);
        Assert.Contains(distributions, d => MediaTypeIri(d)!.EndsWith("/text/csv"));
        Assert.Contains(distributions, d => MediaTypeIri(d)!.EndsWith("/application/ld+json"));

        var email = dataset.GetProperty("dcat:contactPoint").GetProperty("vcard:hasEmail").GetProperty("@id").GetString();
        Assert.Equal("mailto:info@ambiquality.org", email);
    }

    [Fact]
    public async Task Catalog_LiveCsvDistribution_ConformsToCsvwSchema()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var dataset = LiveDataset(doc);

        var csv = dataset.GetProperty("dcat:distribution").EnumerateArray()
            .Single(d => MediaTypeIri(d)!.EndsWith("/text/csv"));

        var conformsTo = csv.GetProperty("dcterms:conformsTo").GetProperty("@id").GetString();
        Assert.EndsWith("/v1/schema/observations.csv-metadata.json", conformsTo);
    }

    [Fact]
    public async Task Catalog_LiveDatasetHasSpatialAndTemporalExtent()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var dataset = LiveDataset(doc);

        // Temporal extent derives from the seeded measurements (2026-05-01).
        var temporal = dataset.GetProperty("dcterms:temporal");
        Assert.StartsWith("2026-05-01", temporal.GetProperty("dcat:startDate").GetProperty("@value").GetString());

        // Spatial coverage is an array: a RÚIAN obec IRI (DCAT-AP-CZ) plus the WKT bbox geometry.
        var spatial = dataset.GetProperty("dcterms:spatial").EnumerateArray().ToList();
        Assert.Contains(spatial, s => RuianObecId(s) is not null);
        var wkt = spatial.Single(s => s.TryGetProperty("dcat:bbox", out _))
            .GetProperty("dcat:bbox").GetProperty("@value").GetString();
        Assert.StartsWith("POLYGON", wkt);
    }

    [Fact]
    public async Task Catalog_SeriesAndMemberDatasets_CarryKeywordSpatialAndPeriodicity()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var series = Series(doc);
        var may = Members(doc).Single(m => m.GetProperty("@id").GetString()!.EndsWith("#dataset-2026-05"));

        // Both the series and the member dataset must carry cs+en keywords and a RÚIAN spatial
        // IRI — the LKOD validator raises errors when these are missing.
        foreach (var node in new[] { series, may })
        {
            var keywords = node.GetProperty("dcat:keyword").EnumerateArray().ToList();
            Assert.Contains(keywords, k => k.GetProperty("@language").GetString() == "cs");
            Assert.Contains(keywords, k => k.GetProperty("@language").GetString() == "en");
            Assert.Contains(node.GetProperty("dcterms:spatial").EnumerateArray(),
                s => RuianObecId(s) is not null);
        }

        // A frozen monthly archive never updates.
        Assert.EndsWith("/frequency/NEVER",
            may.GetProperty("dcterms:accrualPeriodicity").GetProperty("@id").GetString());
    }

    [Fact]
    public async Task Catalog_PublishesMonthlyDatasetSeries()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var series = Series(doc);

        // The series groups the bulk archives; it is updated monthly and points at its ends.
        Assert.EndsWith("/frequency/MONTHLY",
            series.GetProperty("dcterms:accrualPeriodicity").GetProperty("@id").GetString());
        Assert.EndsWith("#dataset-2026-04", series.GetProperty("dcat:first").GetProperty("@id").GetString());
        Assert.EndsWith("#dataset-2026-05", series.GetProperty("dcat:last").GetProperty("@id").GetString());

        // cs + en language-tagged title and description.
        Assert.NotNull(LangValue(series.GetProperty("dcterms:title"), "cs"));
        Assert.NotNull(LangValue(series.GetProperty("dcterms:description"), "en"));
    }

    [Fact]
    public async Task Catalog_HasOneMemberDatasetPerSeededMonth()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var members = Members(doc);

        // Two months seeded (2026-04, 2026-05) -> exactly two member datasets, newest first.
        Assert.Equal(2, members.Count);
        Assert.EndsWith("#dataset-2026-05", members[0].GetProperty("@id").GetString());
        Assert.EndsWith("#dataset-2026-04", members[1].GetProperty("@id").GetString());
    }

    [Fact]
    public async Task Catalog_MemberDataset_LinksToSeriesAndIsBoundedToItsMonth()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var seriesId = Series(doc).GetProperty("@id").GetString();
        var may = Members(doc).Single(m => m.GetProperty("@id").GetString()!.EndsWith("#dataset-2026-05"));

        Assert.Equal(seriesId, may.GetProperty("dcat:inSeries").GetProperty("@id").GetString());

        var temporal = may.GetProperty("dcterms:temporal");
        Assert.StartsWith("2026-05-01", temporal.GetProperty("dcat:startDate").GetProperty("@value").GetString());
        Assert.StartsWith("2026-06-01", temporal.GetProperty("dcat:endDate").GetProperty("@value").GetString());
    }

    [Fact]
    public async Task Catalog_MemberDataset_HasOneGzipFilePerFormat()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var may = Members(doc).Single(m => m.GetProperty("@id").GetString()!.EndsWith("#dataset-2026-05"));

        var distributions = may.GetProperty("dcat:distribution").EnumerateArray().ToList();
        Assert.Equal(2, distributions.Count);

        // Each distribution is a single gzip-compressed file — no zip container, no multi-file split.
        Assert.All(distributions, d =>
        {
            Assert.Equal("application/gzip", d.GetProperty("dcat:compressFormat").GetString());
            Assert.EndsWith(".gz", d.GetProperty("dcat:downloadURL").GetProperty("@id").GetString());
            Assert.Contains("gzip", d.GetProperty("dcterms:title").GetString());
            Assert.DoesNotContain("zip,", d.GetProperty("dcterms:title").GetString());
        });

        Assert.Contains(distributions, d => MediaTypeIri(d)!.EndsWith("/text/csv"));
        Assert.Contains(distributions, d => MediaTypeIri(d)!.EndsWith("/application/ld+json"));

        // accessURL is DCAT-AP-mandatory and, for a downloadable file, equals the downloadURL.
        Assert.All(distributions, d =>
            Assert.Equal(d.GetProperty("dcat:downloadURL").GetProperty("@id").GetString(),
                         d.GetProperty("dcat:accessURL").GetProperty("@id").GetString()));
    }

    // --- helpers -----------------------------------------------------------------

    private static JsonElement Datasets(JsonElement doc) => doc.GetProperty("dcat:dataset");

    /// <summary>The continuous live dataset (its @id ends in "#dataset", not "#dataset-YYYY-MM").</summary>
    private static JsonElement LiveDataset(JsonElement doc) =>
        Datasets(doc).EnumerateArray().Single(d =>
            d.GetProperty("@type").GetString() == "dcat:Dataset" &&
            d.GetProperty("@id").GetString()!.EndsWith("#dataset"));

    private static JsonElement Series(JsonElement doc) =>
        Datasets(doc).EnumerateArray().Single(d => d.GetProperty("@type").GetString() == "dcat:DatasetSeries");

    /// <summary>The monthly member datasets, newest first (@id contains "#dataset-").</summary>
    private static List<JsonElement> Members(JsonElement doc) =>
        Datasets(doc).EnumerateArray()
            .Where(d => d.GetProperty("@type").GetString() == "dcat:Dataset"
                     && d.GetProperty("@id").GetString()!.Contains("#dataset-"))
            .ToList();

    private static List<JsonElement> AllDistributions(JsonElement doc) =>
        Datasets(doc).EnumerateArray()
            .Where(d => d.TryGetProperty("dcat:distribution", out _))
            .SelectMany(d => d.GetProperty("dcat:distribution").EnumerateArray())
            .ToList();

    /// <summary>The IANA media-type IRI (dcat:mediaType @id) of a distribution.</summary>
    private static string? MediaTypeIri(JsonElement distribution) =>
        distribution.GetProperty("dcat:mediaType").GetProperty("@id").GetString();

    /// <summary>The RÚIAN obec IRI of a dcterms:spatial element, or null when it is not a RÚIAN ref.</summary>
    private static string? RuianObecId(JsonElement spatial) =>
        spatial.TryGetProperty("@id", out var id) &&
        id.GetString() is { } s && s.Contains("/ruian/obec/") ? s : null;

    /// <summary>Extract the @value for a given language tag from a JSON-LD language-tagged literal array.</summary>
    private static string? LangValue(JsonElement node, string lang) =>
        node.EnumerateArray()
            .Where(e => e.GetProperty("@language").GetString() == lang)
            .Select(e => e.GetProperty("@value").GetString())
            .FirstOrDefault();
}
