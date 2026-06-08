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
        Assert.Equal("Praha", building.GetProperty("address").GetProperty("municipalityName").GetString());
        Assert.Equal("office", building.GetProperty("buildingTypeCode").GetString());
        // Open data: coordinates are precise (no anonymization).
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
    public async Task GetBuilding_ExposesFullOfnAddressAndPreciseCoordinates()
    {
        var b = await Client.GetFromJsonAsync<JsonElement>($"/v1/buildings/{EvidenceSeed.BuildingStreetId}");

        var addr = b.GetProperty("address");
        Assert.Equal(70010002, addr.GetProperty("addressPointCode").GetInt64());
        Assert.Equal("Václavské náměstí", addr.GetProperty("streetName").GetString());
        Assert.Equal(837, addr.GetProperty("houseNumber").GetInt32());
        Assert.Equal("č.p.", addr.GetProperty("houseNumberType").GetString());
        Assert.Equal(56, addr.GetProperty("orientationNumber").GetInt32());
        Assert.Equal("Praha", addr.GetProperty("municipalityName").GetString());
        Assert.Equal("11000", addr.GetProperty("psc").GetString());

        // Open data: coordinates are precise (no anonymization).
        Assert.Equal(50.081234, b.GetProperty("latitude").GetDouble(), 5);
        Assert.Equal(14.427891, b.GetProperty("longitude").GetDouble(), 5);
    }

    [Fact]
    public async Task GetBuilding_JsonLd_EmitsConformantOfnAdresaNode()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/buildings/{EvidenceSeed.BuildingId}");
        request.Headers.Add("Accept", "application/ld+json");
        var response = await Client.SendAsync(request);

        Assert.Equal("application/ld+json", response.Content.Headers.ContentType?.MediaType);
        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        var addr = doc.GetProperty("ambiq:address");
        Assert.Equal("Adresa", addr.GetProperty("typ").GetString());
        Assert.Equal(
            "https://linked.cuzk.cz/resource/ruian/adresni-misto/70010001",
            addr.GetProperty("adresní_místo").GetString());
        Assert.Equal("Karlovo náměstí", addr.GetProperty("název_ulice").GetString());
        Assert.Equal(1, addr.GetProperty("číslo_domovní").GetInt32());
        Assert.Equal("č.p.", addr.GetProperty("typ_čísla_domovního").GetString());
        Assert.Equal("Praha", addr.GetProperty("název_obce").GetString());
        Assert.Equal("11000", addr.GetProperty("psč").GetString());
        Assert.Contains("Praha", addr.GetProperty("text").GetProperty("cs").GetString());
    }

    [Fact]
    public async Task BadBbox_Returns400Problem()
    {
        var response = await Client.GetAsync("/v1/buildings?bbox=not,a,box");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
