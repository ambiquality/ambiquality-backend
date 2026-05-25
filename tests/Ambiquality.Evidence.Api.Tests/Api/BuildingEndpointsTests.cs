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

    [Fact]
    public async Task RegisterBuilding_WithValidData_Returns201Created()
    {
        var request = new RegisterBuildingRequest(
            UriSlug: "test-building-001",
            Name: "Test Building",
            Street: "123 Main St",
            City: "Prague",
            Postcode: "12000",
            Country: "CZ",
            BuildingTypeCode: "HOUSE",
            Latitude: 50.0755,
            Longitude: 14.4378,
            AnonymizationLevel: "precise",
            YearBuilt: 2000,
            YearRenovated: null);

        var response = await _client.PostAsJsonAsync("/buildings", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegisterBuildingResult>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("test-building-001", result.UriSlug);
    }

    [Fact]
    public async Task RegisterBuilding_WithDuplicateSlug_Returns409Conflict()
    {
        var request = new RegisterBuildingRequest(
            UriSlug: "duplicate-building",
            Name: "Building 1",
            Street: "123 Main St",
            City: "Prague",
            Postcode: "12000",
            Country: "CZ",
            BuildingTypeCode: "HOUSE",
            Latitude: 50.0755,
            Longitude: 14.4378,
            AnonymizationLevel: "precise",
            YearBuilt: 2000,
            YearRenovated: null);

        // Register first building
        var response1 = await _client.PostAsJsonAsync("/buildings", request);
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);

        // Attempt to register duplicate
        var response2 = await _client.PostAsJsonAsync("/buildings", request);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    [Fact]
    public async Task ChangeBuildingName_WithValidData_Returns204NoContent()
    {
        // First register a building
        var registerRequest = new RegisterBuildingRequest(
            UriSlug: "building-for-name-change",
            Name: "Original Name",
            Street: "123 Main St",
            City: "Prague",
            Postcode: "12000",
            Country: "CZ",
            BuildingTypeCode: "HOUSE",
            Latitude: 50.0755,
            Longitude: 14.4378,
            AnonymizationLevel: "precise",
            YearBuilt: 2000,
            YearRenovated: null);

        var registerResponse = await _client.PostAsJsonAsync("/buildings", registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var building = await registerResponse.Content.ReadFromJsonAsync<RegisterBuildingResult>();
        Assert.NotNull(building);

        // Now change the name
        var changeRequest = new ChangeBuildingNameRequest(
            NewName: "New Name",
            ValidFrom: DateTime.UtcNow.AddHours(1));

        var response = await _client.PutAsJsonAsync($"/buildings/{building.Id}/name", changeRequest);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ChangeBuildingAddress_WithValidData_Returns204NoContent()
    {
        // First register a building
        var registerRequest = new RegisterBuildingRequest(
            UriSlug: "building-for-address-change",
            Name: "Building Name",
            Street: "123 Old St",
            City: "Prague",
            Postcode: "12000",
            Country: "CZ",
            BuildingTypeCode: "HOUSE",
            Latitude: 50.0755,
            Longitude: 14.4378,
            AnonymizationLevel: "precise",
            YearBuilt: 2000,
            YearRenovated: null);

        var registerResponse = await _client.PostAsJsonAsync("/buildings", registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var building = await registerResponse.Content.ReadFromJsonAsync<RegisterBuildingResult>();
        Assert.NotNull(building);

        // Now change the address
        var changeRequest = new ChangeBuildingAddressRequest(
            Street: "456 New St",
            City: "Brno",
            Postcode: "60000",
            Country: "CZ",
            ValidFrom: DateTime.UtcNow.AddHours(1));

        var response = await _client.PutAsJsonAsync($"/buildings/{building.Id}/address", changeRequest);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RegisterBuilding_WithInvalidAnonymizationLevel_Returns400BadRequest()
    {
        var request = new RegisterBuildingRequest(
            UriSlug: "bad-anonymization",
            Name: "Test Building",
            Street: "123 Main St",
            City: "Prague",
            Postcode: "12000",
            Country: "CZ",
            BuildingTypeCode: "HOUSE",
            Latitude: 50.0755,
            Longitude: 14.4378,
            AnonymizationLevel: "invalid_level",
            YearBuilt: 2000,
            YearRenovated: null);

        var response = await _client.PostAsJsonAsync("/buildings", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeBuildingName_WithNonexistentBuilding_Returns404NotFound()
    {
        var changeRequest = new ChangeBuildingNameRequest(
            NewName: "New Name",
            ValidFrom: DateTime.UtcNow.AddHours(1));

        var response = await _client.PutAsJsonAsync($"/buildings/{Guid.NewGuid()}/name", changeRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBuildingById_WithValidId_Returns200WithData()
    {
        var building = await RegisterBuildingAsync("building-get-by-id", "Gettable Building");

        var response = await _client.GetAsync($"/buildings/{building.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<BuildingSnapshotResponse>();
        Assert.NotNull(snapshot);
        Assert.Equal(building.Id, snapshot.Id);
        Assert.Equal("building-get-by-id", snapshot.UriSlug);
        Assert.Equal("Gettable Building", snapshot.Name);
        Assert.Equal("Prague", snapshot.City);
        Assert.Equal("HOUSE", snapshot.BuildingTypeCode);
        Assert.Equal("precise", snapshot.AnonymizationLevel);
    }

    [Fact]
    public async Task GetBuildingBySlug_WithValidSlug_Returns200WithData()
    {
        await RegisterBuildingAsync("building-get-by-slug", "Slug Building");

        var response = await _client.GetAsync("/buildings/building-get-by-slug");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<BuildingSnapshotResponse>();
        Assert.NotNull(snapshot);
        Assert.Equal("building-get-by-slug", snapshot.UriSlug);
        Assert.Equal("Slug Building", snapshot.Name);
    }

    [Fact]
    public async Task GetBuildingById_WithNonexistentId_Returns404NotFound()
    {
        var response = await _client.GetAsync($"/buildings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBuildingById_WithInvalidAsOf_Returns400BadRequest()
    {
        var building = await RegisterBuildingAsync("building-bad-asof", "As-Of Building");

        var response = await _client.GetAsync($"/buildings/{building.Id}?asOf=not-a-timestamp");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

        [Fact]
    public async Task ChangeBuildingName_IdenticalRePut_IsIdempotent()
    {
        var building = await RegisterBuildingAsync("building-idempotent-name", "Idem Building");

        // A fixed, microsecond-clean instant so the value round-trips through
        // Postgres (tstzrange has microsecond resolution) byte-identically; the
        // second PUT then compares equal and short-circuits to a no-op.
        var validFrom = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var changeRequest = new ChangeBuildingNameRequest(NewName: "Renamed", ValidFrom: validFrom);

        // First PUT applies the change.
        var first = await _client.PutAsJsonAsync($"/buildings/{building.Id}/name", changeRequest);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // Identical re-PUT (same value AND same validFrom) is a silent no-op.
        var second = await _client.PutAsJsonAsync($"/buildings/{building.Id}/name", changeRequest);
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
        var getResponse = await _client.GetAsync($"/buildings/{building.Id}?asOf={asOf}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var snapshot = await getResponse.Content.ReadFromJsonAsync<BuildingSnapshotResponse>();
        Assert.NotNull(snapshot);
        Assert.Equal("Renamed", snapshot.Name);
    }

    private async Task<RegisterBuildingResult> RegisterBuildingAsync(string slug, string name)
    {
        var request = new RegisterBuildingRequest(
            UriSlug: slug,
            Name: name,
            Street: "123 Main St",
            City: "Prague",
            Postcode: "12000",
            Country: "CZ",
            BuildingTypeCode: "HOUSE",
            Latitude: 50.0755,
            Longitude: 14.4378,
            AnonymizationLevel: "precise",
            YearBuilt: 2000,
            YearRenovated: null);

        var response = await _client.PostAsJsonAsync("/buildings", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegisterBuildingResult>();
        Assert.NotNull(result);
        return result;
    }
}
