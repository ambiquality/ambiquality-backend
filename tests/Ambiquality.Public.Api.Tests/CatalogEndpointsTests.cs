using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;

namespace Ambiquality.Public.Api.Tests;

public sealed class CatalogEndpointsTests(TimescaleFixture fixture) : PublicApiTestBase(fixture)
{
    [Fact]
    public async Task ListBuildings_ReturnsSeededBuilding()
    {
        var page = await Client.GetFromJsonAsync<JsonElement>("/v1/buildings");
        var items = page.GetProperty("items").EnumerateArray().ToList();
        var building = Assert.Single(items, b => b.GetProperty("id").GetString() == EvidenceSeed.BuildingId);

        Assert.Equal("Test Tower", building.GetProperty("name").GetString());
        Assert.Equal("Praha", building.GetProperty("address").GetProperty("city").GetString());
        Assert.Equal("office", building.GetProperty("buildingTypeCode").GetString());
        // anonymization 'precise' → coordinates unmasked.
        Assert.Equal(50.087465, building.GetProperty("latitude").GetDouble(), 5);
    }

    [Fact]
    public async Task ListBuildings_FilterByType_FiltersOut()
    {
        var page = await Client.GetFromJsonAsync<JsonElement>("/v1/buildings?buildingType=school");
        Assert.Empty(page.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task GetBuildingById_Missing_Returns404()
    {
        var response = await Client.GetAsync($"/v1/buildings/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BuildingRooms_Nested_ReturnsRoom()
    {
        var page = await Client.GetFromJsonAsync<JsonElement>($"/v1/buildings/{EvidenceSeed.BuildingId}/rooms");
        var room = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("Lab 1", room.GetProperty("name").GetString());
        Assert.Equal("long", room.GetProperty("exposureCode").GetString());
        Assert.Contains("traffic", room.GetProperty("pollutionSources").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task RoomSensors_Nested_ReturnsSensorWithQudt()
    {
        var page = await Client.GetFromJsonAsync<JsonElement>($"/v1/rooms/{EvidenceSeed.RoomId}/sensors");
        var sensor = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("active", sensor.GetProperty("statusCode").GetString());

        var parameters = sensor.GetProperty("measuredParameters").EnumerateArray().ToList();
        Assert.Equal(2, parameters.Count);
        Assert.Contains(parameters, p => p.GetProperty("code").GetString() == "co2"
            && p.GetProperty("quantityKindUri").GetString()!.Contains("AmountOfSubstanceFraction"));
    }

    [Fact]
    public async Task GetSensorById_ReturnsSensor()
    {
        var sensor = await Client.GetFromJsonAsync<JsonElement>($"/v1/sensors/{EvidenceSeed.SensorId}");
        Assert.Equal("Acme", sensor.GetProperty("manufacturer").GetString());
        Assert.Equal(EvidenceSeed.RoomId, sensor.GetProperty("roomId").GetString());
    }

    [Fact]
    public async Task GetBuilding_StreetAnon_ExposesStreetAndCityButNotPostcode()
    {
        var b = await Client.GetFromJsonAsync<JsonElement>($"/v1/buildings/{EvidenceSeed.BuildingStreetId}");

        var addr = b.GetProperty("address");
        Assert.Equal("Wenceslas Square", addr.GetProperty("street").GetString());
        Assert.Equal("Prague", addr.GetProperty("city").GetString());
        Assert.Equal("CZ", addr.GetProperty("country").GetString());
        Assert.Equal(JsonValueKind.Null, addr.GetProperty("postcode").ValueKind);

        // Coordinates coarsened to 3 dp (≈110 m).
        Assert.Equal(50.081, b.GetProperty("latitude").GetDouble(), 3);
        Assert.Equal(14.428, b.GetProperty("longitude").GetDouble(), 3);
    }

    [Fact]
    public async Task GetBuilding_MunicipalityAnon_ExposesOnlyCityAndCountry()
    {
        var b = await Client.GetFromJsonAsync<JsonElement>($"/v1/buildings/{EvidenceSeed.BuildingMunicipalityId}");

        var addr = b.GetProperty("address");
        Assert.Equal(JsonValueKind.Null, addr.GetProperty("street").ValueKind);
        Assert.Equal("Prague", addr.GetProperty("city").GetString());
        Assert.Equal("CZ", addr.GetProperty("country").GetString());
        Assert.Equal(JsonValueKind.Null, addr.GetProperty("postcode").ValueKind);

        // Coordinates coarsened to 2 dp (≈1.1 km).
        Assert.Equal(50.09, b.GetProperty("latitude").GetDouble(), 2);
        Assert.Equal(14.40, b.GetProperty("longitude").GetDouble(), 2);
    }

    [Fact]
    public async Task BadBbox_Returns400Problem()
    {
        var response = await Client.GetAsync("/v1/buildings?bbox=not,a,box");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
