using System.Net;
using System.Net.Http.Json;
using Ambiquality.Evidence.Api.Api;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Tests.Infrastructure;
using Ambiquality.Evidence.Api.Tests.TestSupport;

namespace Ambiquality.Evidence.Api.Tests.Api;

/// <summary>
/// End-to-end coverage of JWT-gated mutations, building ownership enforcement,
/// and the lazy user-projection upsert. Coordinates are open data and returned
/// precisely to every reader (there is no anonymization). The test auth scheme
/// authenticates the default client as the owner; a second client acts as a
/// different user and a third is anonymous.
/// </summary>
public sealed class AuthorizationTests : IAsyncLifetime
{
    private const double Latitude = 50.0755123;
    private const double Longitude = 14.4378456;

    private EvidenceApiFactory _factory = null!;
    private HttpClient _owner = null!;
    private HttpClient _otherUser = null!;
    private HttpClient _anonymous = null!;

    public async Task InitializeAsync()
    {
        _factory = new EvidenceApiFactory();
        await _factory.InitializeAsync();

        _owner = _factory.CreateClient();

        _otherUser = _factory.CreateClient();
        _otherUser.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, Guid.NewGuid().ToString());

        _anonymous = _factory.CreateClient();
        _anonymous.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");
    }

    public async Task DisposeAsync()
    {
        _owner.Dispose();
        _otherUser.Dispose();
        _anonymous.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Mutation_WhenAnonymous_Returns401()
    {
        var response = await _anonymous.PostAsJsonAsync("/v1/buildings", BuildingRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangeBuilding_ByNonOwner_Returns403()
    {
        var building = await RegisterAsOwnerAsync();

        var change = new ChangeBuildingNameRequest("Hijacked", DateTime.UtcNow.AddHours(1));
        var response = await _otherUser.PutAsJsonAsync($"/v1/buildings/{building.Id}/name", change);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeBuilding_ByOwner_Returns204()
    {
        var building = await RegisterAsOwnerAsync();

        var change = new ChangeBuildingNameRequest("Renamed", DateTime.UtcNow.AddHours(1));
        var response = await _owner.PutAsJsonAsync($"/v1/buildings/{building.Id}/name", change);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetBuilding_ReturnsPreciseCoordinatesToEveryReader()
    {
        var building = await RegisterAsOwnerAsync();

        foreach (var client in new[] { _owner, _otherUser, _anonymous })
        {
            var snapshot = await ReadSnapshot(client, building.Id);
            Assert.Equal(Latitude, snapshot.Latitude!.Value, 6);
            Assert.Equal(Longitude, snapshot.Longitude!.Value, 6);
        }
    }

    [Fact]
    public async Task UserProjection_IsStablePerSub_AndDistinctAcrossUsers()
    {
        var b1 = await RegisterAsOwnerAsync();
        var b2 = await RegisterAsOwnerAsync();

        var s1 = await ReadSnapshot(_owner, b1.Id);
        var s2 = await ReadSnapshot(_owner, b2.Id);
        Assert.Equal(s1.OwnerId, s2.OwnerId); // same sub -> same projection row

        var otherResponse = await _otherUser.PostAsJsonAsync("/v1/buildings", BuildingRequest());
        var b3 = (await otherResponse.Content.ReadFromJsonAsync<RegisterBuildingResult>())!;
        var s3 = await ReadSnapshot(_owner, b3.Id);
        Assert.NotEqual(s1.OwnerId, s3.OwnerId); // different sub -> different projection row
    }

    [Fact]
    public async Task ListBuildings_WhenAnonymous_Returns401()
    {
        var response = await _anonymous.GetAsync("/v1/buildings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListBuildings_AsOwner_ReturnsOnlyOwnBuildingsWithPreciseCoordinates()
    {
        var mine1 = await RegisterAsOwnerAsync();
        var mine2 = await RegisterAsOwnerAsync();

        // A building owned by a different user must not appear in the owner's list.
        var otherResponse = await _otherUser.PostAsJsonAsync("/v1/buildings", BuildingRequest());
        var theirs = (await otherResponse.Content.ReadFromJsonAsync<RegisterBuildingResult>())!;

        var response = await _owner.GetAsync("/v1/buildings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<List<BuildingSnapshotResponse>>())!;

        var ids = list.Select(b => b.Id).ToHashSet();
        Assert.Contains(mine1.Id, ids);
        Assert.Contains(mine2.Id, ids);
        Assert.DoesNotContain(theirs.Id, ids);

        var mine = list.First(b => b.Id == mine1.Id);
        Assert.Equal(Latitude, mine.Latitude!.Value, 6);
        Assert.Equal(Longitude, mine.Longitude!.Value, 6);
    }

    private async Task<RegisterBuildingResult> RegisterAsOwnerAsync()
    {
        var response = await _owner.PostAsJsonAsync("/v1/buildings", BuildingRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterBuildingResult>())!;
    }

    private static async Task<BuildingSnapshotResponse> ReadSnapshot(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/v1/buildings/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BuildingSnapshotResponse>())!;
    }

    private static RegisterBuildingRequest BuildingRequest() =>
        new(
            Name: "Auth Test Building",
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
            Latitude: Latitude,
            Longitude: Longitude,
            YearBuilt: 2000,
            YearRenovated: null);
}
