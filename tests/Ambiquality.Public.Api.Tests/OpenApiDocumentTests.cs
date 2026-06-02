using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;

namespace Ambiquality.Public.Api.Tests;

/// <summary>
/// The generated OpenAPI document is a published deliverable (F15), so it must
/// carry self-describing metadata: title, version, the CC BY 4.0 license that the
/// open data is released under, and the externally-reachable server URL derived
/// from PublicApi:BaseIri (so Scalar "Try it" / generated clients target the real
/// deployment behind Caddy).
/// </summary>
public sealed class OpenApiDocumentTests(TimescaleFixture fixture) : PublicApiTestBase(fixture)
{
    [Fact]
    public async Task OpenApiDocument_IsServed()
    {
        var response = await Client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_HasTitleVersionAndCcByLicense()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        var info = doc.GetProperty("info");
        Assert.Equal("Ambiquality Public API", info.GetProperty("title").GetString());
        Assert.Equal("v1", info.GetProperty("version").GetString());

        var license = info.GetProperty("license");
        Assert.Equal("CC BY 4.0", license.GetProperty("name").GetString());
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/", license.GetProperty("url").GetString());
    }

    [Fact]
    public async Task OpenApiDocument_AdvertisesConfiguredServer()
    {
        var doc = await Client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        // Factory sets PublicApi:BaseIri = "https://data.test.example"; document
        // paths already carry /v1, so the server URL is the bare origin.
        var server = doc.GetProperty("servers").EnumerateArray().Single();
        Assert.Equal(PublicApiFactory.BaseIri, server.GetProperty("url").GetString());
    }
}
