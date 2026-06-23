using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;

namespace Ambiquality.Public.Api.Tests;

public sealed class DcatCatalogTests(TimescaleFixture fixture) : PublicApiTestBase(fixture)
{
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
    public async Task Catalog_IsDcatCatalogWithDataset()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");

        Assert.Equal("dcat:Catalog", doc.GetProperty("@type").GetString());
        var dataset = doc.GetProperty("dcat:dataset");
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
    public async Task Catalog_DatasetHasThemeKeywordAndPeriodicityFromCodelists()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var dataset = doc.GetProperty("dcat:dataset");

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
    public async Task Catalog_DistributionsCarryFileTypeFormat()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var distributions = doc.GetProperty("dcat:dataset").GetProperty("dcat:distribution").EnumerateArray().ToList();

        // Every distribution advertises both dcat:mediaType and the EU file-type dcterms:format.
        Assert.All(distributions, d =>
            Assert.Contains("/file-type/", d.GetProperty("dcterms:format").GetProperty("@id").GetString()));
    }

    /// <summary>Extract the @value for a given language tag from a JSON-LD language-tagged literal array.</summary>
    private static string? LangValue(JsonElement node, string lang) =>
        node.EnumerateArray()
            .Where(e => e.GetProperty("@language").GetString() == lang)
            .Select(e => e.GetProperty("@value").GetString())
            .FirstOrDefault();

    [Fact]
    public async Task Catalog_HasTwoDistributionsAndContactPoint()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var dataset = doc.GetProperty("dcat:dataset");

        var distributions = dataset.GetProperty("dcat:distribution").EnumerateArray().ToList();
        Assert.Equal(2, distributions.Count);
        Assert.Contains(distributions, d => d.GetProperty("dcat:mediaType").GetString() == "text/csv");
        Assert.Contains(distributions, d => d.GetProperty("dcat:mediaType").GetString() == "application/ld+json");

        var email = dataset.GetProperty("dcat:contactPoint").GetProperty("vcard:hasEmail").GetProperty("@id").GetString();
        Assert.Equal("mailto:info@ambiquality.org", email);
    }

    [Fact]
    public async Task Catalog_CsvDistribution_ConformsToCsvwSchema()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var dataset = doc.GetProperty("dcat:dataset");

        var csv = dataset.GetProperty("dcat:distribution").EnumerateArray()
            .Single(d => d.GetProperty("dcat:mediaType").GetString() == "text/csv");

        var conformsTo = csv.GetProperty("dcterms:conformsTo").GetProperty("@id").GetString();
        Assert.EndsWith("/v1/schema/observations.csv-metadata.json", conformsTo);
    }

    [Fact]
    public async Task Catalog_HasSpatialAndTemporalExtent()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");
        var dataset = doc.GetProperty("dcat:dataset");

        // Temporal extent derives from the seeded measurements (2026-05-01).
        var temporal = dataset.GetProperty("dcterms:temporal");
        Assert.StartsWith("2026-05-01", temporal.GetProperty("dcat:startDate").GetProperty("@value").GetString());

        // Spatial extent derives from the seeded building coordinates.
        var wkt = dataset.GetProperty("dcterms:spatial").GetProperty("dcat:bbox").GetProperty("@value").GetString();
        Assert.StartsWith("POLYGON", wkt);
    }
}
