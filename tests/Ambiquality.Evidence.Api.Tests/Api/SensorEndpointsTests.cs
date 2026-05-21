using System.Net;
using System.Net.Http.Json;
using Ambiquality.Evidence.Api.Api;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Tests.Infrastructure;

namespace Ambiquality.Evidence.Api.Tests.Api;

public sealed class SensorEndpointsTests : IAsyncLifetime
{
    private EvidenceApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _buildingId;
    private Guid _roomId;

    public async Task InitializeAsync()
    {
        _factory = new EvidenceApiFactory();
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();

        var buildingRequest = new
        {
            UriSlug = "test-building-sensors",
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

        var buildingResponse = await _client.PostAsJsonAsync("/buildings", buildingRequest);
        var building = await buildingResponse.Content.ReadFromJsonAsync<RegisterBuildingResult>();
        _buildingId = building?.Id ?? throw new InvalidOperationException("Failed to create test building");

        _roomId = await CreateRoomAsync("sensor-host-room");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<Guid> CreateRoomAsync(string slug)
    {
        var roomRequest = new RegisterRoomRequest(
            UriSlug: slug,
            Name: "Host Room",
            Floor: 1,
            FunctionCode: null,
            ExposureCode: null,
            AreaM2: null,
            CeilingHeightM: null,
            VentilationType: null,
            PollutionSources: Array.Empty<string>());

        var response = await _client.PostAsJsonAsync($"/buildings/{_buildingId}/rooms", roomRequest);
        var room = await response.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        return room!.Id;
    }

    private async Task<SensorSnapshotResponse> RegisterSensorAsync(
        string slug,
        string statusCode = "active",
        string[]? parameters = null)
    {
        var request = new RegisterSensorRequest(
            UriSlug: slug,
            Manufacturer: "Aranet",
            Model: "Aranet4",
            SerialNumber: "SN-0001",
            StatusCode: statusCode,
            MeasuredParameters: parameters ?? new[] { "co2", "temperature" });

        var response = await _client.PostAsJsonAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SensorSnapshotResponse>())!;
    }

    [Fact]
    public async Task RegisterSensor_WithValidData_Returns201Created()
    {
        var sensor = await RegisterSensorAsync("aranet4-101");

        Assert.NotEqual(Guid.Empty, sensor.Id);
        Assert.Equal("aranet4-101", sensor.UriSlug);
        Assert.Equal(_roomId, sensor.RoomId);
        Assert.Equal(_buildingId, sensor.BuildingId);
        Assert.Equal("active", sensor.StatusCode);
        Assert.Contains("co2", sensor.MeasuredParameters);
    }

    [Fact]
    public async Task GetSensorById_WithValidId_Returns200Ok()
    {
        var registered = await RegisterSensorAsync("aranet4-202");

        var response = await _client.GetAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sensor = await response.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Equal(registered.Id, sensor!.Id);
        Assert.Equal("Aranet4", sensor.Model);
    }

    [Fact]
    public async Task GetSensorBySlug_WithValidSlug_Returns200Ok()
    {
        await RegisterSensorAsync("aranet4-303");

        var response = await _client.GetAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/aranet4-303");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sensor = await response.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Equal("aranet4-303", sensor!.UriSlug);
    }

    [Fact]
    public async Task GetSensorById_WithAsOf_ReturnsSnapshotAtTime()
    {
        var registered = await RegisterSensorAsync("aranet4-404");
        var asOf = registered.AsOf.ToString("o");

        var response = await _client.GetAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}?asOf={Uri.EscapeDataString(asOf)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sensor = await response.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Equal("active", sensor!.StatusCode);
    }

    [Fact]
    public async Task GetSensorById_WithNonexistentId_Returns404NotFound()
    {
        var response = await _client.GetAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSensorById_FromWrongRoom_Returns404NotFound()
    {
        var registered = await RegisterSensorAsync("aranet4-505");
        var otherRoom = Guid.NewGuid();

        var response = await _client.GetAsync(
            $"/buildings/{_buildingId}/rooms/{otherRoom}/sensors/{registered.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSensorIdentity_WithValidData_Returns204NoContent()
    {
        var registered = await RegisterSensorAsync("aranet4-606");

        var request = new ChangeSensorIdentityRequest("Aranet", "Aranet4 Pro", "SN-9999", DateTime.UtcNow);
        var response = await _client.PutAsJsonAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/identity", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var get = await _client.GetAsync($"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        var sensor = await get.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Equal("Aranet4 Pro", sensor!.Model);
        Assert.Equal("SN-9999", sensor.SerialNumber);
    }

    [Fact]
    public async Task ChangeSensorStatus_WithValidData_Returns204NoContent()
    {
        var registered = await RegisterSensorAsync("aranet4-707");

        var request = new ChangeSensorStatusRequest("maintenance", DateTime.UtcNow);
        var response = await _client.PutAsJsonAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/status", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var get = await _client.GetAsync($"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        var sensor = await get.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Equal("maintenance", sensor!.StatusCode);
    }

    [Fact]
    public async Task ChangeSensorPlacement_MovesSensorToAnotherRoom()
    {
        var registered = await RegisterSensorAsync("aranet4-808");
        var targetRoom = await CreateRoomAsync("sensor-target-room");

        var request = new ChangeSensorPlacementRequest(targetRoom, DateTime.UtcNow);
        var response = await _client.PutAsJsonAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/placement", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Now visible under the target room...
        var inTarget = await _client.GetAsync(
            $"/buildings/{_buildingId}/rooms/{targetRoom}/sensors/{registered.Id}");
        Assert.Equal(HttpStatusCode.OK, inTarget.StatusCode);
        var moved = await inTarget.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Equal(targetRoom, moved!.RoomId);

        // ...and no longer in the original room.
        var inOriginal = await _client.GetAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        Assert.Equal(HttpStatusCode.NotFound, inOriginal.StatusCode);
    }

    [Fact]
    public async Task ChangeSensorPlacement_ToNonexistentRoom_Returns404NotFound()
    {
        var registered = await RegisterSensorAsync("aranet4-818");

        var request = new ChangeSensorPlacementRequest(Guid.NewGuid(), DateTime.UtcNow);
        var response = await _client.PutAsJsonAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/placement", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMeasuredParameter_WithValidData_Returns200Ok()
    {
        var registered = await RegisterSensorAsync("aranet4-909", parameters: new[] { "co2" });

        var request = new AddMeasuredParameterRequest("humidity", DateTime.UtcNow);
        var response = await _client.PostAsJsonAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/measured-parameters", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var get = await _client.GetAsync($"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        var sensor = await get.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Contains("humidity", sensor!.MeasuredParameters);
    }

    [Fact]
    public async Task RemoveMeasuredParameter_WithValidData_Returns200Ok()
    {
        var registered = await RegisterSensorAsync("aranet4-910", parameters: new[] { "co2", "voc" });

        var url = $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/measured-parameters/voc"
                + $"?validTo={Uri.EscapeDataString(DateTime.UtcNow.ToString("o"))}";
        var response = await _client.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var get = await _client.GetAsync($"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        var sensor = await get.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.DoesNotContain("voc", sensor!.MeasuredParameters);
        Assert.Contains("co2", sensor.MeasuredParameters);
    }

    [Fact]
    public async Task RegisterSensor_WithUnknownStatusCode_Returns400BadRequest()
    {
        var request = new RegisterSensorRequest(
            UriSlug: "aranet4-badstatus",
            Manufacturer: "Aranet",
            Model: "Aranet4",
            SerialNumber: "SN-1",
            StatusCode: "flying",
            MeasuredParameters: new[] { "co2" });

        var response = await _client.PostAsJsonAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterSensor_WithUnknownParameterCode_Returns400BadRequest()
    {
        var request = new RegisterSensorRequest(
            UriSlug: "aranet4-badparam",
            Manufacturer: "Aranet",
            Model: "Aranet4",
            SerialNumber: "SN-1",
            StatusCode: "active",
            MeasuredParameters: new[] { "radiation" });

        var response = await _client.PostAsJsonAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSensorStatus_WithNonAdvancingValidFrom_Returns400BadRequest()
    {
        var registered = await RegisterSensorAsync("aranet4-badvalidfrom");

        var request = new ChangeSensorStatusRequest("maintenance", DateTime.UtcNow.AddYears(-1));
        var response = await _client.PutAsJsonAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/status", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSensorStatus_OnNonexistentSensor_Returns404NotFound()
    {
        var request = new ChangeSensorStatusRequest("maintenance", DateTime.UtcNow.AddHours(1));
        var response = await _client.PutAsJsonAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{Guid.NewGuid()}/status", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSensorById_WithInvalidAsOf_Returns400BadRequest()
    {
        var registered = await RegisterSensorAsync("aranet4-badasof");

        var response = await _client.GetAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}?asOf=not-a-timestamp");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddMeasuredParameter_OverlappingSameParameter_Returns409Conflict()
    {
        var registered = await RegisterSensorAsync("aranet4-overlap", parameters: new[] { "co2" });

        // co2 already has an open [created, +inf) row; adding it again with an
        // overlapping validity must trip the GiST exclusion constraint.
        var request = new AddMeasuredParameterRequest("co2", DateTime.UtcNow.AddHours(1));
        var response = await _client.PostAsJsonAsync(
            $"/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/measured-parameters", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
