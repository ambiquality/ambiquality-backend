using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;

namespace Ambiquality.Public.Api.Tests;

public sealed class CodelistEndpointsTests(TimescaleFixture fixture) : PublicApiTestBase(fixture)
{
    [Fact]
    public async Task Index_ListsAllSchemes()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/codelists");
        var schemes = doc.GetProperty("schemes").EnumerateArray()
            .Select(s => s.GetProperty("scheme").GetString())
            .ToList();

        Assert.Contains("building-type", schemes);
        Assert.Contains("room-function", schemes);
        Assert.Contains("ventilation-type", schemes);
        Assert.Contains("pollution-source", schemes);
        Assert.Contains("exposure", schemes);
        Assert.Contains("sensor-status", schemes);
    }

    [Fact]
    public async Task Scheme_ReturnsConceptsWithBilingualLabels()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/codelists/building-type");
        var concepts = doc.GetProperty("concepts").EnumerateArray().ToList();

        var office = Assert.Single(concepts, c => c.GetProperty("code").GetString() == "office");
        Assert.Equal("Office building", office.GetProperty("labelEn").GetString());
        Assert.Equal("Administrativní budova", office.GetProperty("labelCs").GetString());
    }

    [Fact]
    public async Task Concept_PlainJson_CarriesCodeAndLabels()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/v1/codelists/ventilation-type/mechanical");
        Assert.Equal("mechanical", doc.GetProperty("code").GetString());
        Assert.Equal("Nucené větrání", doc.GetProperty("labelCs").GetString());
        Assert.EndsWith("/v1/codelists/ventilation-type", doc.GetProperty("schemeIri").GetString());
    }

    [Fact]
    public async Task Concept_JsonLd_IsSkosConceptWithLanguageTaggedPrefLabel()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/codelists/exposure/long");
        request.Headers.Add("Accept", "application/ld+json");
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/ld+json", response.Content.Headers.ContentType?.MediaType);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("skos:Concept", doc.GetProperty("@type").GetString());
        Assert.Equal("long", doc.GetProperty("skos:notation").GetString());

        var langs = doc.GetProperty("skos:prefLabel").EnumerateArray()
            .Select(l => l.GetProperty("@language").GetString())
            .ToList();
        Assert.Contains("cs", langs);
        Assert.Contains("en", langs);

        Assert.EndsWith("/v1/codelists/exposure", doc.GetProperty("skos:inScheme").GetProperty("@id").GetString());
    }

    [Theory]
    [InlineData("/v1/codelists/no-such-scheme")]
    [InlineData("/v1/codelists/building-type/no-such-code")]
    public async Task Unknown_Returns404(string path)
    {
        var response = await Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BuildingJsonLd_BuildingType_ReferencesCodelistConcept()
    {
        // The catalog JSON-LD now points code attributes at dereferenceable SKOS concepts.
        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/buildings/{EvidenceSeed.BuildingId}");
        request.Headers.Add("Accept", "application/ld+json");
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var type = doc.GetProperty("ambiq:buildingType");
        Assert.EndsWith("/v1/codelists/building-type/office", type.GetProperty("@id").GetString());
        Assert.Equal("office", type.GetProperty("skos:notation").GetString());
    }
}
