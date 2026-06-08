using System.Net;
using System.Net.Http.Json;
using Ambiquality.Evidence.Api.Api;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Tests.Infrastructure;

namespace Ambiquality.Evidence.Api.Tests.Api;

/// <summary>
/// Every read endpoint must answer HEAD with the same status code as GET but an
/// empty body (RFC 9110 §9.3.2), so consumers can do cheap existence/freshness
/// probes.
/// </summary>
public sealed class HeadMethodTests : IAsyncLifetime
{
    private EvidenceApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _buildingId;
    private string _buildingSlug = null!;
    private Guid _roomId;
    private string _roomSlug = null!;

    public async Task InitializeAsync()
    {
        _factory = new EvidenceApiFactory();
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();

        var buildingRequest = new RegisterBuildingRequest(
            Name: "Head Test Building",
            AddressPointCode: 21794547,
            StreetName: "Náměstí Winstona Churchilla",
            HouseNumber: 1938,
            HouseNumberType: "č.p.",
            OrientationNumber: 4,
            OrientationNumberLetter: null,
            MunicipalityName: "Praha",
            MunicipalityPartName: "Žižkov",
            Psc: "13067",
            DistrictName: "Hlavní město Praha",
            RegionName: "Hlavní město Praha",
            BuildingTypeCode: "family_house",
            Latitude: 50.0755,
            Longitude: 14.4378,
            YearBuilt: 2000,
            YearRenovated: null);

        var buildingResponse = await _client.PostAsJsonAsync("/v1/buildings", buildingRequest);
        var building = await buildingResponse.Content.ReadFromJsonAsync<RegisterBuildingResult>();
        _buildingId = building!.Id;
        _buildingSlug = building.UriSlug;

        var roomRequest = new RegisterRoomRequest(
            Name: "Head Test Room",
            Floor: 1,
            FunctionCode: "office",
            ExposureCode: "medium",
            AreaM2: 40.0,
            CeilingHeightM: 3.0,
            VentilationType: "mechanical",
            PollutionSources: Array.Empty<string>());

        var roomResponse = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", roomRequest);
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        _roomId = room!.Id;
        _roomSlug = room.UriSlug;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task AssertHeadMatchesGet(string url, HttpStatusCode expectedStatus)
    {
        var getResponse = await _client.GetAsync(url);
        Assert.Equal(expectedStatus, getResponse.StatusCode);

        using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
        var headResponse = await _client.SendAsync(headRequest);

        Assert.Equal(getResponse.StatusCode, headResponse.StatusCode);

        var headBody = await headResponse.Content.ReadAsByteArrayAsync();
        Assert.Empty(headBody);
    }

    [Fact]
    public Task Head_BuildingById_Existing_MatchesGetWithEmptyBody() =>
        AssertHeadMatchesGet($"/v1/buildings/{_buildingId}", HttpStatusCode.OK);

    [Fact]
    public Task Head_BuildingBySlug_Existing_MatchesGetWithEmptyBody() =>
        AssertHeadMatchesGet($"/v1/buildings/{_buildingSlug}", HttpStatusCode.OK);

    [Fact]
    public Task Head_BuildingById_Missing_Returns404() =>
        AssertHeadMatchesGet($"/v1/buildings/{Guid.NewGuid()}", HttpStatusCode.NotFound);

    [Fact]
    public Task Head_RoomById_Existing_MatchesGetWithEmptyBody() =>
        AssertHeadMatchesGet($"/v1/buildings/{_buildingId}/rooms/{_roomId}", HttpStatusCode.OK);

    [Fact]
    public Task Head_RoomBySlug_Existing_MatchesGetWithEmptyBody() =>
        AssertHeadMatchesGet($"/v1/buildings/{_buildingId}/rooms/{_roomSlug}", HttpStatusCode.OK);

    [Fact]
    public Task Head_RoomById_Missing_Returns404() =>
        AssertHeadMatchesGet($"/v1/buildings/{_buildingId}/rooms/{Guid.NewGuid()}", HttpStatusCode.NotFound);
}
