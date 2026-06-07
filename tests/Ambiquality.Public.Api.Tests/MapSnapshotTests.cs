using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace Ambiquality.Public.Api.Tests;

public sealed class MapSnapshotTests(TimescaleFixture fixture) : PublicApiTestBase(fixture)
{
    // The seed's measurements are dated 2026-05-01, well before "now", so with the default
    // 15-minute freshness window every marker is stale. A client wired with a very large
    // freshness window treats the seed as fresh, exercising the mean/latest-value path.
    private HttpClient FreshClient() =>
        Factory.WithWebHostBuilder(b =>
            b.UseSetting("PublicApi:MapSnapshotFreshnessSeconds", "100000000")).CreateClient();

    [Fact]
    public async Task Snapshot_MissingParameterCode_Returns400()
    {
        var response = await Client.GetAsync("/v1/map/snapshot");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Snapshot_Co2_ReturnsOnlyBuildingsWithActiveSensors()
    {
        var snapshot = await Client.GetFromJsonAsync<JsonElement>("/v1/map/snapshot?parameterCode=co2");

        Assert.Equal("co2", snapshot.GetProperty("parameterCode").GetString());
        // Only the seeded "Test Tower" carries an active co2 sensor; the street/municipality
        // buildings have no sensors and so are absent (inner join on sensors).
        var item = Assert.Single(snapshot.GetProperty("items").EnumerateArray());
        Assert.Equal(EvidenceSeed.BuildingId, item.GetProperty("buildingId").GetString());
        Assert.Equal("bld-test", item.GetProperty("slug").GetString());
        Assert.Equal("Test Tower", item.GetProperty("name").GetString());
        Assert.Equal(1, item.GetProperty("sensorCount").GetInt32());
        // anonymization 'precise' → coordinates unmasked.
        Assert.Equal(50.087465, item.GetProperty("lat").GetDouble(), 5);
    }

    [Fact]
    public async Task Snapshot_DefaultFreshness_MarksSeedStaleWithNullValue()
    {
        var snapshot = await Client.GetFromJsonAsync<JsonElement>("/v1/map/snapshot?parameterCode=co2");
        var item = Assert.Single(snapshot.GetProperty("items").EnumerateArray());

        Assert.True(item.GetProperty("stale").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("latestValue").ValueKind);
        // observedAt is still the freshest contributing observation (the latest valid co2 row).
        Assert.Equal(
            new DateTime(2026, 5, 1, 11, 0, 0, DateTimeKind.Utc),
            item.GetProperty("observedAt").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task Snapshot_WithinFreshness_MeansLatestPerSensor()
    {
        using var client = FreshClient();
        var snapshot = await client.GetFromJsonAsync<JsonElement>("/v1/map/snapshot?parameterCode=co2");
        var item = Assert.Single(snapshot.GetProperty("items").EnumerateArray());

        Assert.False(item.GetProperty("stale").GetBoolean());
        // One sensor → mean is its latest valid co2 value: M3 (420) wins the (received_at, id)
        // tie-break over M2 (410); the invalid M4 (430) is excluded.
        Assert.Equal(420, item.GetProperty("latestValue").GetDouble(), 3);
        Assert.Equal("ppm", snapshot.GetProperty("unit").GetString());
    }

    [Fact]
    public async Task Snapshot_BboxExcludingBuilding_ReturnsNoItems()
    {
        // A bbox over the Atlantic excludes Prague.
        var snapshot = await Client.GetFromJsonAsync<JsonElement>(
            "/v1/map/snapshot?parameterCode=co2&bbox=-30,30,-20,40");
        Assert.Empty(snapshot.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Snapshot_BboxIncludingBuilding_ReturnsItem()
    {
        var snapshot = await Client.GetFromJsonAsync<JsonElement>(
            "/v1/map/snapshot?parameterCode=co2&bbox=14,50,15,51");
        Assert.Single(snapshot.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Snapshot_InvalidBbox_Returns400()
    {
        var response = await Client.GetAsync("/v1/map/snapshot?parameterCode=co2&bbox=1,2,3");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
