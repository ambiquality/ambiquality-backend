using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;

namespace Ambiquality.Public.Api.Tests;

public sealed class ObservationEndpointsTests(TimescaleFixture fixture) : PublicApiTestBase(fixture)
{
    [Fact]
    public async Task List_DefaultExcludesInvalid()
    {
        var page = await Client.GetFromJsonAsync<JsonElement>("/v1/observations");

        var ids = Ids(page);
        Assert.Equal(4, ids.Count); // M1, M2, M3, M5 (M4 is invalid)
        Assert.DoesNotContain(EvidenceSeed.M4, ids);
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/", page.GetProperty("license").GetString());
    }

    [Fact]
    public async Task List_FilterByParameterCode_ReturnsOnlyMatching()
    {
        var page = await Client.GetFromJsonAsync<JsonElement>("/v1/observations?parameterCode=co2");
        var ids = Ids(page);
        Assert.Equal([EvidenceSeed.M1, EvidenceSeed.M2, EvidenceSeed.M3], ids.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task List_IncludeInvalid_IncludesInvalidRow()
    {
        var page = await Client.GetFromJsonAsync<JsonElement>("/v1/observations?parameterCode=co2&includeInvalid=true");
        Assert.Contains(EvidenceSeed.M4, Ids(page));
    }

    [Fact]
    public async Task List_FilterByBuildingId_ResolvesSensors()
    {
        var page = await Client.GetFromJsonAsync<JsonElement>($"/v1/observations?buildingId={EvidenceSeed.BuildingId}&parameterCode=co2");
        Assert.Equal(3, Ids(page).Count);
    }

    [Fact]
    public async Task List_FilterByUnknownBuilding_ReturnsEmpty()
    {
        var page = await Client.GetFromJsonAsync<JsonElement>($"/v1/observations?buildingId={Guid.NewGuid()}");
        Assert.Empty(Ids(page));
    }

    [Fact]
    public async Task List_KeysetPaging_TieBreak_NoDuplicatesOrSkips()
    {
        // M2 and M3 share received_at; paging one row at a time must not skip or repeat.
        var collected = new List<string>();
        var url = "/v1/observations?parameterCode=co2&limit=1";

        for (var i = 0; i < 10; i++)
        {
            var page = await Client.GetFromJsonAsync<JsonElement>(url);
            var ids = Ids(page);
            if (ids.Count == 0) break;
            collected.AddRange(ids);

            if (page.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String)
                url = next.GetString()!.Replace(PublicApiFactory.BaseIri + "/v1", "/v1");
            else
                break;
        }

        // Ordered received_at DESC, id DESC → M3 (T2), M2 (T2), M1 (T1).
        Assert.Equal([EvidenceSeed.M3, EvidenceSeed.M2, EvidenceSeed.M1], collected);
        Assert.Equal(collected.Count, collected.Distinct().Count());
    }

    [Fact]
    public async Task GetById_Existing_Returns200WithQudt()
    {
        var observation = await Client.GetFromJsonAsync<JsonElement>($"/v1/observations/{EvidenceSeed.M1}");
        Assert.Equal(400, observation.GetProperty("value").GetDouble());
        Assert.Equal("http://qudt.org/vocab/quantitykind/AmountOfSubstanceFraction",
            observation.GetProperty("quantityKindUri").GetString());
    }

    [Fact]
    public async Task GetById_Missing_Returns404Problem()
    {
        var response = await Client.GetAsync($"/v1/observations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task List_SetsCacheAndDescribedByHeaders()
    {
        var response = await Client.GetAsync("/v1/observations");
        Assert.Equal("public, max-age=300", response.Headers.CacheControl?.ToString()
            ?? response.Headers.GetValues("Cache-Control").FirstOrDefault());
        Assert.Contains(response.Headers.GetValues("Link"), v => v.Contains("rel=\"describedby\""));
    }

    private static List<string> Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()!).ToList();
}
