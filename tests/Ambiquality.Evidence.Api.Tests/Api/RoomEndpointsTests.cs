using System.Net;
using System.Net.Http.Json;
using System.Text;
using Ambiquality.Evidence.Api.Api;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Application.Rooms;
using Ambiquality.Evidence.Api.Tests.Infrastructure;
using Ambiquality.Evidence.Api.Tests.TestSupport;

namespace Ambiquality.Evidence.Api.Tests.Api;

public sealed class RoomEndpointsTests : IAsyncLifetime
{
    // Server-generated slug shape: prefix + 8-char base32 token (see RandomSlugGenerator).
    private const string SlugPattern = "^rm-[a-z0-9]{8}$";

    private EvidenceApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _buildingId;

    public async Task InitializeAsync()
    {
        _factory = new EvidenceApiFactory();
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();

        // Create a test building for room tests
        var buildingRequest = new
        {
            Name = "Test Building",
            Street = "123 Main St",
            City = "Prague",
            Postcode = "12000",
            Country = "CZ",
            BuildingTypeCode = "HOUSE",
            Latitude = 50.0755,
            Longitude = 14.4378,
            AnonymizationLevel = "precise",
            YearBuilt = 2000,
            YearRenovated = (int?)null
        };

        var response = await _client.PostAsJsonAsync("/v1/buildings", buildingRequest);
        var buildingResult = await response.Content.ReadFromJsonAsync<RegisterBuildingResult>();
        _buildingId = buildingResult?.Id ?? throw new InvalidOperationException("Failed to create test building");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task RegisterRoom_WithValidData_Returns201Created()
    {
        var request = new RegisterRoomRequest(
            Name: "Conference Room",
            Floor: 1,
            FunctionCode: "conference",
            ExposureCode: "medium",
            AreaM2: 50.0,
            CeilingHeightM: 3.0,
            VentilationType: "mechanical",
            PollutionSources: new[] { "traffic" });

        var response = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Matches(SlugPattern, result.UriSlug);
        Assert.Equal("Conference Room", result.Name);
        Assert.Equal(1, result.Floor);
    }

    [Fact]
    public async Task ListRooms_AsOwner_ReturnsBuildingsRooms()
    {
        async Task RegisterRoomAsync(string name) =>
            (await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", new RegisterRoomRequest(
                Name: name, Floor: 1, FunctionCode: null, ExposureCode: null,
                AreaM2: null, CeilingHeightM: null, VentilationType: null,
                PollutionSources: Array.Empty<string>()))).EnsureSuccessStatusCode();

        await RegisterRoomAsync("List Room A");
        await RegisterRoomAsync("List Room B");

        var response = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rooms = (await response.Content.ReadFromJsonAsync<List<RoomSnapshotResponse>>())!;

        Assert.Equal(2, rooms.Count);
        Assert.Contains(rooms, r => r.Name == "List Room A");
        Assert.Contains(rooms, r => r.Name == "List Room B");
        Assert.All(rooms, r => Assert.Equal(_buildingId, r.BuildingId));
    }

    [Fact]
    public async Task ListRooms_ByNonOwner_Returns403()
    {
        using var otherUser = _factory.CreateClient();
        otherUser.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, Guid.NewGuid().ToString());

        var response = await otherUser.GetAsync($"/v1/buildings/{_buildingId}/rooms");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRoomById_WithValidId_Returns200Ok()
    {
        // Register a room first
        var registerRequest = new RegisterRoomRequest(
            Name: "Lab Room",
            Floor: 2,
            FunctionCode: "lab",
            ExposureCode: "medium",
            AreaM2: 75.0,
            CeilingHeightM: 3.5,
            VentilationType: "mechanical",
            PollutionSources: new[] { "chemicals" });

        var registerResponse = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", registerRequest);
        var registeredRoom = await registerResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        var roomId = registeredRoom!.Id;

        // Now retrieve it by ID
        var getResponse = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{roomId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var retrievedRoom = await getResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        Assert.NotNull(retrievedRoom);
        Assert.Equal(roomId, retrievedRoom.Id);
        Assert.Equal(registeredRoom.UriSlug, retrievedRoom.UriSlug);
        Assert.Equal("Lab Room", retrievedRoom.Name);
        Assert.Equal(2, retrievedRoom.Floor);
    }

    [Fact]
    public async Task GetRoomBySlug_WithValidSlug_Returns200Ok()
    {
        // Register a room first
        var registerRequest = new RegisterRoomRequest(
            Name: "Storage Room",
            Floor: 3,
            FunctionCode: "storage",
            ExposureCode: "medium",
            AreaM2: 120.0,
            CeilingHeightM: 2.7,
            VentilationType: "natural",
            PollutionSources: Array.Empty<string>());

        var registerResponse = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", registerRequest);
        var registeredRoom = await registerResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();

        // Now retrieve it by its server-generated slug
        var getResponse = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{registeredRoom!.UriSlug}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var retrievedRoom = await getResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        Assert.NotNull(retrievedRoom);
        Assert.Equal(registeredRoom.UriSlug, retrievedRoom.UriSlug);
        Assert.Equal("Storage Room", retrievedRoom.Name);
        Assert.Equal(3, retrievedRoom.Floor);
    }

    [Fact]
    public async Task GetRoomById_WithAsOf_ReturnsSnapshotAtTime()
    {
        // Register a room first
        var registerRequest = new RegisterRoomRequest(
            Name: "Original Office Name",
            Floor: 4,
            FunctionCode: "office",
            ExposureCode: "medium",
            AreaM2: 60.0,
            CeilingHeightM: 3.0,
            VentilationType: "mechanical",
            PollutionSources: Array.Empty<string>());

        var registerResponse = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", registerRequest);
        var registeredRoom = await registerResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        var roomId = registeredRoom!.Id;
        var registrationTime = registeredRoom.AsOf;

        // Retrieve at the time of registration
        var asOfQuery = registrationTime.ToString("o");
        var getResponse = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{roomId}?asOf={Uri.EscapeDataString(asOfQuery)}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var retrievedRoom = await getResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        Assert.NotNull(retrievedRoom);
        Assert.Equal("Original Office Name", retrievedRoom.Name);
    }

    [Fact]
    public async Task GetRoomById_WithNonexistentId_Returns404NotFound()
    {
        var nonexistentId = Guid.NewGuid();
        var getResponse = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{nonexistentId}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetRoomBySlug_WithNonexistentSlug_Returns404NotFound()
    {
        var getResponse = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/nonexistent-slug");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetRoomById_WithWrongBuildingId_Returns404NotFound()
    {
        // Register a room
        var registerRequest = new RegisterRoomRequest(
            Name: "Test Room",
            Floor: 5,
            FunctionCode: "office",
            ExposureCode: "medium",
            AreaM2: 50.0,
            CeilingHeightM: 3.0,
            VentilationType: "mechanical",
            PollutionSources: Array.Empty<string>());

        var registerResponse = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", registerRequest);
        var registeredRoom = await registerResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        var roomId = registeredRoom!.Id;

        // Try to retrieve with wrong building ID
        var wrongBuildingId = Guid.NewGuid();
        var getResponse = await _client.GetAsync($"/v1/buildings/{wrongBuildingId}/rooms/{roomId}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task ChangeRoomName_WithValidData_Returns204NoContent()
    {
        // Register a room first
        var registerRequest = new RegisterRoomRequest(
            Name: "Original Name",
            Floor: 6,
            FunctionCode: "office",
            ExposureCode: "medium",
            AreaM2: 45.0,
            CeilingHeightM: 3.0,
            VentilationType: "mechanical",
            PollutionSources: Array.Empty<string>());

        var registerResponse = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", registerRequest);
        var registeredRoom = await registerResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        var roomId = registeredRoom!.Id;

        // Change the room name
        var changeRequest = new ChangeRoomAttributeRequest(
            NewValue: "Updated Name",
            ValidFrom: DateTime.UtcNow);

        var changeResponse = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{roomId}/name",
            changeRequest);

        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);

        // Verify the change by retrieving the room
        var getResponse = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{roomId}");
        var updatedRoom = await getResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        Assert.Equal("Updated Name", updatedRoom!.Name);
    }

    [Fact]
    public async Task ChangeRoomFloor_WithValidData_Returns204NoContent()
    {
        // Register a room first
        var registerRequest = new RegisterRoomRequest(
            Name: "Floor Test Room",
            Floor: 1,
            FunctionCode: null,
            ExposureCode: null,
            AreaM2: null,
            CeilingHeightM: null,
            VentilationType: null,
            PollutionSources: Array.Empty<string>());

        var registerResponse = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", registerRequest);
        var registeredRoom = await registerResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        var roomId = registeredRoom!.Id;

        // Change the floor
        var changeRequest = new ChangeRoomFloorRequest(
            Floor: 3,
            ValidFrom: DateTime.UtcNow);

        var changeResponse = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{roomId}/floor",
            changeRequest);

        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);

        // Verify the change
        var getResponse = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{roomId}");
        var updatedRoom = await getResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        Assert.Equal(3, updatedRoom!.Floor);
    }

    [Fact]
    public async Task AddPollutionSource_WithValidData_Returns204NoContent()
    {
        // Register a room first
        var registerRequest = new RegisterRoomRequest(
            Name: "Pollution Test Room",
            Floor: 2,
            FunctionCode: "lab",
            ExposureCode: null,
            AreaM2: null,
            CeilingHeightM: null,
            VentilationType: null,
            PollutionSources: Array.Empty<string>());

        var registerResponse = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", registerRequest);
        var registeredRoom = await registerResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        var roomId = registeredRoom!.Id;

        // Add pollution source
        var addRequest = new AddPollutionSourceRequest(
            SourceCode: "chemicals",
            ValidFrom: DateTime.UtcNow);

        var addResponse = await _client.PostAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{roomId}/pollution-sources",
            addRequest);

        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);

        // Verify the source was added
        var getResponse = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{roomId}");
        var updatedRoom = await getResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        Assert.Contains("chemicals", updatedRoom!.PollutionSources);
    }

    [Fact]
    public async Task RemovePollutionSource_WithValidData_Returns204NoContent()
    {
        // Register a room with pollution source
        var registerRequest = new RegisterRoomRequest(
            Name: "Remove Source Test",
            Floor: 3,
            FunctionCode: null,
            ExposureCode: null,
            AreaM2: null,
            CeilingHeightM: null,
            VentilationType: null,
            PollutionSources: new[] { "traffic" });

        var registerResponse = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", registerRequest);
        var registeredRoom = await registerResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        var roomId = registeredRoom!.Id;

        // Verify source exists
        var beforeRemoval = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{roomId}");
        var beforeRoom = await beforeRemoval.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        Assert.Contains("traffic", beforeRoom!.PollutionSources);

        // Close the pollution source's validity via PUT with validTo in the body
        var removeResponse = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{roomId}/pollution-sources/traffic",
            new RemovePollutionSourceRequest(ValidTo: DateTime.UtcNow));

        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        // Verify source was removed at current time
        var afterRemoval = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{roomId}");
        var afterRoom = await afterRemoval.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        Assert.DoesNotContain("traffic", afterRoom!.PollutionSources);
    }

    [Fact]
    public async Task ChangeRoomName_WithNonAdvancingValidFrom_Returns400BadRequest()
    {
        var registerRequest = new RegisterRoomRequest(
            Name: "Original Name",
            Floor: 1,
            FunctionCode: null,
            ExposureCode: null,
            AreaM2: null,
            CeilingHeightM: null,
            VentilationType: null,
            PollutionSources: Array.Empty<string>());

        var registerResponse = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", registerRequest);
        var registeredRoom = await registerResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        var roomId = registeredRoom!.Id;

        // A valid-from before the current open range's start violates the
        // advancing-validity rule: a domain rule violation, i.e. 400 — not 404.
        var changeRequest = new ChangeRoomAttributeRequest(
            NewValue: "Updated Name",
            ValidFrom: DateTime.UtcNow.AddYears(-1));

        var changeResponse = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{roomId}/name",
            changeRequest);

        Assert.Equal(HttpStatusCode.BadRequest, changeResponse.StatusCode);
    }

    [Fact]
    public async Task ChangeRoomName_OnNonexistentRoom_Returns404NotFound()
    {
        var changeRequest = new ChangeRoomAttributeRequest(
            NewValue: "Updated Name",
            ValidFrom: DateTime.UtcNow.AddHours(1));

        var changeResponse = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{Guid.NewGuid()}/name",
            changeRequest);

        Assert.Equal(HttpStatusCode.NotFound, changeResponse.StatusCode);
    }

    [Fact]
    public async Task GetRoomById_WithInvalidAsOf_Returns400BadRequest()
    {
        var registerRequest = new RegisterRoomRequest(
            Name: "As-Of Room",
            Floor: 1,
            FunctionCode: null,
            ExposureCode: null,
            AreaM2: null,
            CeilingHeightM: null,
            VentilationType: null,
            PollutionSources: Array.Empty<string>());

        var registerResponse = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", registerRequest);
        var registeredRoom = await registerResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        var roomId = registeredRoom!.Id;

        var getResponse = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{roomId}?asOf=not-a-timestamp");

        Assert.Equal(HttpStatusCode.BadRequest, getResponse.StatusCode);
    }

    // Raw JSON floor tokens that the typed ChangeRoomFloorRequest(byte Floor)
    // must reject as a client error (problem+json), never a 500. The first three
    // fail framework byte-binding; "200" binds as a byte but trips the endpoint's
    // 0–100 domain guard.
    [Theory]
    [InlineData("\"abc\"")] // non-numeric → byte binding 400
    [InlineData("-1")]      // negative → byte binding 400
    [InlineData("300")]     // > 255 → byte binding 400
    [InlineData("200")]     // valid byte but > 100 → domain guard 400
    public async Task ChangeRoomFloor_WithInvalidValue_Returns400ProblemJson(string floorJson)
    {
        var registerRequest = new RegisterRoomRequest(
            Name: "Bad Floor Room",
            Floor: 1,
            FunctionCode: null,
            ExposureCode: null,
            AreaM2: null,
            CeilingHeightM: null,
            VentilationType: null,
            PollutionSources: Array.Empty<string>());

        var registerResponse = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", registerRequest);
        var registeredRoom = await registerResponse.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        var roomId = registeredRoom!.Id;

        var body = $$"""{"floor": {{floorJson}}, "validFrom": "{{DateTime.UtcNow:o}}"}""";
        var changeResponse = await _client.PutAsync(
            $"/v1/buildings/{_buildingId}/rooms/{roomId}/floor",
            new StringContent(body, Encoding.UTF8, "application/json"));

        // A malformed floor must be rejected at the edge as a client error
        // (problem+json), never escape as a 500.
        Assert.True(
            changeResponse.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity,
            $"Expected 400/422 but got {(int)changeResponse.StatusCode}.");
        Assert.Equal(
            "application/problem+json",
            changeResponse.Content.Headers.ContentType?.MediaType);
    }
}
