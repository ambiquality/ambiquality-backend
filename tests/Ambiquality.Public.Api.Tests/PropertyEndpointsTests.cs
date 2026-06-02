using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;

namespace Ambiquality.Public.Api.Tests;

/// <summary>
/// The observable-property vocabulary endpoints — the dereferenceable targets of
/// every measurement's sosa:observedProperty. These need no seeded measurements;
/// the vocabulary is static.
/// </summary>
public sealed class PropertyEndpointsTests(TimescaleFixture fixture) : PublicApiTestBase(fixture)
{
    [Fact]
    public async Task List_ReturnsAllEighteenProperties()
    {
        var collection = await Client.GetFromJsonAsync<JsonElement>("/v1/properties");

        Assert.Equal(18, collection.GetProperty("items").GetArrayLength());
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/",
            collection.GetProperty("license").GetString());
    }

    [Fact]
    public async Task GetByCode_Pm25_HasSpecificMatchKindAndUnit()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/properties/pm2_5");
        request.Headers.Add("Accept", "application/ld+json");
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/ld+json", response.Content.Headers.ContentType?.MediaType);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal($"{PublicApiFactory.BaseIri}/v1/properties/pm2_5", doc.GetProperty("@id").GetString());
        // Distinct, authoritative external match (EEA/EIONET PM2.5).
        Assert.Equal("http://dd.eionet.europa.eu/vocabulary/aq/pollutant/6001",
            doc.GetProperty("skos:exactMatch").GetProperty("@id").GetString());
        // QUDT dimensional kind + applicable unit, in their correct slots.
        Assert.Equal("http://qudt.org/vocab/quantitykind/MassDensity",
            doc.GetProperty("qudt:hasQuantityKind").GetProperty("@id").GetString());
        Assert.Equal("http://qudt.org/vocab/unit/MicroGM-PER-M3",
            doc.GetProperty("qudt:applicableUnit").GetProperty("@id").GetString());
    }

    [Fact]
    public async Task GetByCode_Pm25AndPm10_AreDistinctEvenThoughSameQuantityKind()
    {
        var pm25 = await Client.GetFromJsonAsync<JsonElement>("/v1/properties/pm2_5");
        var pm10 = await Client.GetFromJsonAsync<JsonElement>("/v1/properties/pm10");

        Assert.NotEqual(pm25.GetProperty("iri").GetString(), pm10.GetProperty("iri").GetString());
        Assert.NotEqual(pm25.GetProperty("exactMatchIri").GetString(), pm10.GetProperty("exactMatchIri").GetString());
        // Yet both share the coarse QUDT dimensional kind — the very ambiguity this fixes.
        Assert.Equal(pm25.GetProperty("quantityKindUri").GetString(), pm10.GetProperty("quantityKindUri").GetString());
    }

    [Fact]
    public async Task GetByCode_NonPollutant_HasQuantityKindButNoExternalMatch()
    {
        var temperature = await Client.GetFromJsonAsync<JsonElement>("/v1/properties/temperature");

        Assert.Equal("http://qudt.org/vocab/quantitykind/Temperature",
            temperature.GetProperty("quantityKindUri").GetString());
        Assert.Null(temperature.GetProperty("exactMatchIri").GetStringOrNull());
        Assert.Null(temperature.GetProperty("closeMatchIri").GetStringOrNull());
    }

    [Fact]
    public async Task GetByCode_Unknown_Returns404Problem()
    {
        var response = await Client.GetAsync("/v1/properties/not_a_parameter");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetByCode_SetsLongCacheLifetime()
    {
        var response = await Client.GetAsync("/v1/properties/co2");
        Assert.Equal("public, max-age=86400", response.Headers.CacheControl?.ToString()
            ?? response.Headers.GetValues("Cache-Control").FirstOrDefault());
    }
}

internal static class JsonElementTestExtensions
{
    public static string? GetStringOrNull(this JsonElement element) =>
        element.ValueKind == JsonValueKind.Null ? null : element.GetString();
}
