using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;

namespace Ambiquality.Public.Api.Tests;

public sealed class ObservationAggregateTests(TimescaleFixture fixture) : PublicApiTestBase(fixture)
{
    // The seeded valid co2 rows: 400@10:00, 410@11:00, 420@11:00 (M4=430 is invalid → excluded).
    private const string Co2Day =
        "parameterCode=co2&from=2026-05-01T00:00:00Z&to=2026-05-02T00:00:00Z&bucket=1h";

    [Fact]
    public async Task Aggregate_MissingParameterCode_Returns400()
    {
        var response = await Client.GetAsync($"/v1/observations/aggregate?buildingId={EvidenceSeed.BuildingId}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Aggregate_MissingTarget_Returns400()
    {
        var response = await Client.GetAsync("/v1/observations/aggregate?parameterCode=co2");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Aggregate_InvalidBucket_Returns400()
    {
        var response = await Client.GetAsync(
            $"/v1/observations/aggregate?parameterCode=co2&buildingId={EvidenceSeed.BuildingId}&bucket=17s");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Aggregate_ByBuilding_BucketsAndStats()
    {
        var agg = await Client.GetFromJsonAsync<JsonElement>(
            $"/v1/observations/aggregate?buildingId={EvidenceSeed.BuildingId}&{Co2Day}");

        Assert.Equal("co2", agg.GetProperty("parameterCode").GetString());
        Assert.Equal("ppm", agg.GetProperty("unit").GetString());
        Assert.Equal("1h", agg.GetProperty("bucket").GetString());

        var buckets = agg.GetProperty("buckets").EnumerateArray().ToList();
        Assert.Equal(2, buckets.Count); // 10:00 and 11:00

        var hour10 = buckets[0];
        Assert.Equal(1, hour10.GetProperty("count").GetInt64());
        Assert.Equal(400, hour10.GetProperty("avg").GetDouble(), 3);

        var hour11 = buckets[1];
        Assert.Equal(2, hour11.GetProperty("count").GetInt64());
        Assert.Equal(415, hour11.GetProperty("avg").GetDouble(), 3);
        Assert.Equal(410, hour11.GetProperty("min").GetDouble(), 3);
        Assert.Equal(420, hour11.GetProperty("max").GetDouble(), 3);

        var stats = agg.GetProperty("stats");
        Assert.Equal(3, stats.GetProperty("count").GetInt64());
        Assert.Equal(400, stats.GetProperty("min").GetDouble(), 3);
        Assert.Equal(420, stats.GetProperty("max").GetDouble(), 3);
        Assert.Equal(410, stats.GetProperty("avg").GetDouble(), 3);
        // percentile_cont(0.95) interpolates over [400,410,420]: 410 + 0.9*(420-410) = 419.
        Assert.Equal(419, stats.GetProperty("p95").GetDouble(), 3);
    }

    [Fact]
    public async Task Aggregate_BySensor_MatchesBuilding()
    {
        var agg = await Client.GetFromJsonAsync<JsonElement>(
            $"/v1/observations/aggregate?sensorId={EvidenceSeed.SensorId}&{Co2Day}");
        // The seed's only sensor lives in the seed's only building, so drilling into the
        // sensor gives the same three valid co2 observations.
        Assert.Equal(3, agg.GetProperty("stats").GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task Aggregate_EmptyWindow_StatsNull()
    {
        var agg = await Client.GetFromJsonAsync<JsonElement>(
            "/v1/observations/aggregate?parameterCode=co2&buildingId=" + EvidenceSeed.BuildingId
            + "&from=2027-01-01T00:00:00Z&to=2027-01-02T00:00:00Z&bucket=1h");

        Assert.Empty(agg.GetProperty("buckets").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, agg.GetProperty("stats").ValueKind);
    }
}
