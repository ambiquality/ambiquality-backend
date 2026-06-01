using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;

namespace Ambiquality.Public.Api.Tests;

public sealed class DcatCatalogTests(TimescaleFixture fixture) : PublicApiTestBase(fixture)
{
    [Fact]
    public async Task Catalog_IsDcatCatalogWithDataset()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/catalog");

        Assert.Equal("dcat:Catalog", doc.GetProperty("@type").GetString());
        var dataset = doc.GetProperty("dcat:dataset");
        Assert.Equal("dcat:Dataset", dataset.GetProperty("@type").GetString());
        Assert.Equal("Ambiquality IEQ Open Data", dataset.GetProperty("dcterms:title").GetString());
    }

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
