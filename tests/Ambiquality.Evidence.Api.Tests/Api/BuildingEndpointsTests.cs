using System.Net;
using System.Net.Http.Json;
using Ambiquality.Evidence.Api.Api;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Tests.Infrastructure;
using Ambiquality.Evidence.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ambiquality.Evidence.Api.Tests.Api;

public sealed class BuildingEndpointsTests : IAsyncLifetime
{
    // Server-generated slug shape: prefix + 8-char base32 token (see RandomSlugGenerator).
    private const string SlugPattern = "^bld-[a-z0-9]{8}$";

    private EvidenceApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new EvidenceApiFactory();
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private static RegisterBuildingRequest BuildRequest(string name) => new(
        Name: name,
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

    [Fact]
    public async Task RegisterBuilding_WithValidData_Returns201Created()
    {
        var response = await _client.PostAsJsonAsync("/v1/buildings", BuildRequest("Test Building"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegisterBuildingResult>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Matches(SlugPattern, result.UriSlug);
    }

    [Fact]
    public async Task RegisterBuilding_TwiceWithSameData_ProducesDistinctSlugs()
    {
        // Slugs are server-generated, so identical input never collides.
        var first = await RegisterBuildingAsync("Building 1");
        var second = await RegisterBuildingAsync("Building 1");

        Assert.Matches(SlugPattern, first.UriSlug);
        Assert.Matches(SlugPattern, second.UriSlug);
        Assert.NotEqual(first.UriSlug, second.UriSlug);
    }

    [Fact]
    public async Task ChangeBuildingName_WithValidData_Returns204NoContent()
    {
        var building = await RegisterBuildingAsync("Original Name");

        var changeRequest = new ChangeBuildingNameRequest(
            NewName: "New Name",
            ValidFrom: DateTime.UtcNow.AddHours(1));

        var response = await _client.PutAsJsonAsync($"/v1/buildings/{building.Id}/name", changeRequest);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ChangeBuildingAddress_WithValidData_Returns204NoContent()
    {
        var building = await RegisterBuildingAsync("Building Name");

        var changeRequest = new ChangeBuildingAddressRequest(
            AddressPointCode: 25001234,
            StreetName: "Žerotínovo náměstí",
            HouseNumber: 617,
            HouseNumberType: "č.p.",
            OrientationNumber: 9,
            OrientationNumberLetter: null,
            MunicipalityName: "Brno",
            MunicipalityPartName: "Veveří",
            Psc: "60200",
            DistrictName: "Brno-město",
            RegionName: "Jihomoravský kraj",
            ValidFrom: DateTime.UtcNow.AddHours(1));

        var response = await _client.PutAsJsonAsync($"/v1/buildings/{building.Id}/address", changeRequest);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RegisterBuilding_WithInvalidPsc_Returns400BadRequest()
    {
        var request = BuildRequest("Test Building") with { Psc = "not-a-psc" };

        var response = await _client.PostAsJsonAsync("/v1/buildings", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeBuildingName_WithNonexistentBuilding_Returns404NotFound()
    {
        var changeRequest = new ChangeBuildingNameRequest(
            NewName: "New Name",
            ValidFrom: DateTime.UtcNow.AddHours(1));

        var response = await _client.PutAsJsonAsync($"/v1/buildings/{Guid.NewGuid()}/name", changeRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBuildingById_WithValidId_Returns200WithData()
    {
        var building = await RegisterBuildingAsync("Gettable Building");

        var response = await _client.GetAsync($"/v1/buildings/{building.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<BuildingSnapshotResponse>();
        Assert.NotNull(snapshot);
        Assert.Equal(building.Id, snapshot.Id);
        Assert.Equal(building.UriSlug, snapshot.UriSlug);
        Assert.Equal("Gettable Building", snapshot.Name);
        Assert.Equal("Praha", snapshot.MunicipalityName);
        Assert.Equal(1938, snapshot.HouseNumber);
        Assert.Equal(21794547, snapshot.AddressPointCode);
        Assert.Equal("family_house", snapshot.BuildingTypeCode);
    }

    [Fact]
    public async Task GetBuildingBySlug_WithValidSlug_Returns200WithData()
    {
        var building = await RegisterBuildingAsync("Slug Building");

        var response = await _client.GetAsync($"/v1/buildings/{building.UriSlug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<BuildingSnapshotResponse>();
        Assert.NotNull(snapshot);
        Assert.Equal(building.UriSlug, snapshot.UriSlug);
        Assert.Equal("Slug Building", snapshot.Name);
    }

    [Fact]
    public async Task GetBuildingById_WithNonexistentId_Returns404NotFound()
    {
        var response = await _client.GetAsync($"/v1/buildings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBuildingById_WithInvalidAsOf_Returns400BadRequest()
    {
        var building = await RegisterBuildingAsync("As-Of Building");

        var response = await _client.GetAsync($"/v1/buildings/{building.Id}?asOf=not-a-timestamp");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeBuildingName_IdenticalRePut_IsIdempotent()
    {
        var building = await RegisterBuildingAsync("Idem Building");

        // A fixed, microsecond-clean instant so the value round-trips through
        // Postgres (tstzrange has microsecond resolution) byte-identically; the
        // second PUT then compares equal and short-circuits to a no-op.
        var validFrom = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var changeRequest = new ChangeBuildingNameRequest(NewName: "Renamed", ValidFrom: validFrom);

        // First PUT applies the change.
        var first = await _client.PutAsJsonAsync($"/v1/buildings/{building.Id}/name", changeRequest);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // Identical re-PUT (same value AND same validFrom) is a silent no-op.
        var second = await _client.PutAsJsonAsync($"/v1/buildings/{building.Id}/name", changeRequest);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        // The history is not duplicated: original open row was closed and exactly
        // one new row opened - two rows total, not three.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EvidenceDbContext>();
        var persisted = await db.Buildings.FirstAsync(b => b.Id == building.Id);
        Assert.Equal(2, persisted.NameHistory.Count);

        // And the value at/after validFrom reflects the single applied change.
        // (validFrom is in the future, so we must project as-of that instant.)
        var asOf = Uri.EscapeDataString(validFrom.AddSeconds(1).ToString("O"));
        var getResponse = await _client.GetAsync($"/v1/buildings/{building.Id}?asOf={asOf}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var snapshot = await getResponse.Content.ReadFromJsonAsync<BuildingSnapshotResponse>();
        Assert.NotNull(snapshot);
        Assert.Equal("Renamed", snapshot.Name);
    }

    private async Task<RegisterBuildingResult> RegisterBuildingAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/v1/buildings", BuildRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegisterBuildingResult>();
        Assert.NotNull(result);
        return result;
    }
}
