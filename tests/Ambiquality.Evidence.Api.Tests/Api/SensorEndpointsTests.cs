using System.Net;
using System.Net.Http.Json;
using Ambiquality.Evidence.Api.Api;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Tests.Infrastructure;
using Ambiquality.Evidence.Api.Tests.TestSupport;

namespace Ambiquality.Evidence.Api.Tests.Api;

public sealed class SensorEndpointsTests : IAsyncLifetime
{
    // Server-generated slug shape: prefix + 8-char base32 token (see RandomSlugGenerator).
    private const string SlugPattern = "^sns-[a-z0-9]{8}$";

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
            Name = "Test Building",
            AddressPointCode = 21794547L,
            StreetName = "Náměstí Winstona Churchilla",
            HouseNumber = 1938,
            HouseNumberType = "č.p.",
            OrientationNumber = (int?)4,
            OrientationNumberLetter = (string?)null,
            MunicipalityName = "Praha",
            MunicipalityPartName = "Žižkov",
            Psc = "13067",
            DistrictName = "Hlavní město Praha",
            RegionName = "Hlavní město Praha",
            BuildingTypeCode = "family_house",
            Latitude = 50.0755,
            Longitude = 14.4378,
            YearBuilt = 2000,
            YearRenovated = (int?)null
        };

        var buildingResponse = await _client.PostAsJsonAsync("/v1/buildings", buildingRequest);
        var building = await buildingResponse.Content.ReadFromJsonAsync<RegisterBuildingResult>();
        _buildingId = building?.Id ?? throw new InvalidOperationException("Failed to create test building");

        _roomId = await CreateRoomAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<Guid> CreateRoomAsync()
    {
        var roomRequest = new RegisterRoomRequest(
            Name: "Host Room",
            Floor: 1,
            FunctionCode: null,
            ExposureCode: null,
            AreaM2: null,
            CeilingHeightM: null,
            VentilationType: null,
            PollutionSources: Array.Empty<string>());

        var response = await _client.PostAsJsonAsync($"/v1/buildings/{_buildingId}/rooms", roomRequest);
        var room = await response.Content.ReadFromJsonAsync<RoomSnapshotResponse>();
        return room!.Id;
    }

    private async Task<SensorSnapshotResponse> RegisterSensorAsync(
        string statusCode = "active",
        string[]? parameters = null)
    {
        var request = new RegisterSensorRequest(
            Manufacturer: "Aranet",
            Model: "Aranet4",
            SerialNumber: "SN-0001",
            StatusCode: statusCode,
            MeasuredParameters: parameters ?? new[] { "co2", "temperature" });

        var response = await _client.PostAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SensorSnapshotResponse>())!;
    }

    [Fact]
    public async Task RegisterSensor_WithValidData_Returns201Created()
    {
        var sensor = await RegisterSensorAsync();

        Assert.NotEqual(Guid.Empty, sensor.Id);
        Assert.Matches(SlugPattern, sensor.UriSlug);
        Assert.Equal(_roomId, sensor.RoomId);
        Assert.Equal(_buildingId, sensor.BuildingId);
        Assert.Equal("active", sensor.StatusCode);
        Assert.Contains(sensor.MeasuredParameters, p => p.Code == "co2");
    }

    [Fact]
    public async Task ListSensors_AsOwner_ReturnsRoomSensors()
    {
        var a = await RegisterSensorAsync();
        var b = await RegisterSensorAsync();

        var response = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sensors = (await response.Content.ReadFromJsonAsync<List<SensorSnapshotResponse>>())!;

        var ids = sensors.Select(s => s.Id).ToHashSet();
        Assert.Contains(a.Id, ids);
        Assert.Contains(b.Id, ids);
        Assert.All(sensors, s => Assert.Equal(_roomId, s.RoomId));
    }

    [Fact]
    public async Task ListSensors_ByNonOwner_Returns403()
    {
        using var otherUser = _factory.CreateClient();
        otherUser.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, Guid.NewGuid().ToString());

        var response = await otherUser.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegisterSensor_MeasuredParametersIncludeQudtUris()
    {
        var sensor = await RegisterSensorAsync(parameters: new[] { "co2", "temperature" });

        var co2 = sensor.MeasuredParameters.Single(p => p.Code == "co2");
        Assert.Equal("http://qudt.org/vocab/quantitykind/AmountOfSubstanceFraction", co2.QuantityKindUri);
        Assert.Equal("http://qudt.org/vocab/unit/PPM", co2.UnitUri);

        var temp = sensor.MeasuredParameters.Single(p => p.Code == "temperature");
        Assert.Equal("http://qudt.org/vocab/quantitykind/Temperature", temp.QuantityKindUri);
        Assert.Equal("http://qudt.org/vocab/unit/DEG_C", temp.UnitUri);
    }

    [Fact]
    public async Task RegisterSensor_ReturnsPlaintextApiKeyOnce()
    {
        var request = new RegisterSensorRequest(
            Manufacturer: "Aranet",
            Model: "Aranet4",
            SerialNumber: "SN-0001",
            StatusCode: "active",
            MeasuredParameters: new[] { "co2" });

        var response = await _client.PostAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var registered = await response.Content.ReadFromJsonAsync<SensorRegisteredResponse>();
        Assert.NotNull(registered!.ApiKey);
        Assert.StartsWith("amq_sk_", registered.ApiKey);
    }

    [Fact]
    public async Task GetSensor_DoesNotLeakApiKey()
    {
        var registered = await RegisterSensorAsync();

        var response = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("apiKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("amq_sk_", body);
    }

    [Fact]
    public async Task GetSensorById_WithValidId_Returns200Ok()
    {
        var registered = await RegisterSensorAsync();

        var response = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sensor = await response.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Equal(registered.Id, sensor!.Id);
        Assert.Equal("Aranet4", sensor.Model);
    }

    [Fact]
    public async Task GetSensorBySlug_WithValidSlug_Returns200Ok()
    {
        var registered = await RegisterSensorAsync();

        var response = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.UriSlug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sensor = await response.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Equal(registered.UriSlug, sensor!.UriSlug);
    }

    [Fact]
    public async Task GetSensorById_WithAsOf_ReturnsSnapshotAtTime()
    {
        var registered = await RegisterSensorAsync();
        var asOf = registered.AsOf.ToString("o");

        var response = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}?asOf={Uri.EscapeDataString(asOf)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sensor = await response.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Equal("active", sensor!.StatusCode);
    }

    [Fact]
    public async Task GetSensorById_WithNonexistentId_Returns404NotFound()
    {
        var response = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSensorById_FromWrongRoom_Returns404NotFound()
    {
        var registered = await RegisterSensorAsync();
        var otherRoom = Guid.NewGuid();

        var response = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{otherRoom}/sensors/{registered.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSensorIdentity_WithValidData_Returns204NoContent()
    {
        var registered = await RegisterSensorAsync();

        var request = new ChangeSensorIdentityRequest("Aranet", "Aranet4 Pro", "SN-9999", DateTime.UtcNow);
        var response = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/identity", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var get = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        var sensor = await get.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Equal("Aranet4 Pro", sensor!.Model);
        Assert.Equal("SN-9999", sensor.SerialNumber);
    }

    [Fact]
    public async Task ChangeSensorStatus_WithValidData_Returns204NoContent()
    {
        var registered = await RegisterSensorAsync();

        var request = new ChangeSensorStatusRequest("maintenance", DateTime.UtcNow);
        var response = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/status", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var get = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        var sensor = await get.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Equal("maintenance", sensor!.StatusCode);
    }

    [Fact]
    public async Task ChangeSensorPlacement_MovesSensorToAnotherRoom()
    {
        var registered = await RegisterSensorAsync();
        var targetRoom = await CreateRoomAsync();

        var request = new ChangeSensorPlacementRequest(targetRoom, DateTime.UtcNow);
        var response = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/placement", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Now visible under the target room...
        var inTarget = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{targetRoom}/sensors/{registered.Id}");
        Assert.Equal(HttpStatusCode.OK, inTarget.StatusCode);
        var moved = await inTarget.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Equal(targetRoom, moved!.RoomId);

        // ...and no longer in the original room.
        var inOriginal = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        Assert.Equal(HttpStatusCode.NotFound, inOriginal.StatusCode);
    }

    [Fact]
    public async Task ChangeSensorPlacement_ToNonexistentRoom_Returns404NotFound()
    {
        var registered = await RegisterSensorAsync();

        var request = new ChangeSensorPlacementRequest(Guid.NewGuid(), DateTime.UtcNow);
        var response = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/placement", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMeasuredParameter_WithValidData_Returns204NoContent()
    {
        var registered = await RegisterSensorAsync(parameters: new[] { "co2" });

        var request = new AddMeasuredParameterRequest("humidity", DateTime.UtcNow);
        var response = await _client.PostAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/measured-parameters", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var get = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        var sensor = await get.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.Contains(sensor!.MeasuredParameters, p => p.Code == "humidity");
    }

    [Fact]
    public async Task RemoveMeasuredParameter_WithValidData_Returns204NoContent()
    {
        var registered = await RegisterSensorAsync(parameters: new[] { "co2", "voc" });

        // Close the capability's validity via PUT with validTo in the body.
        var response = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/measured-parameters/voc",
            new RemoveMeasuredParameterRequest(ValidTo: DateTime.UtcNow));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var get = await _client.GetAsync($"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        var sensor = await get.Content.ReadFromJsonAsync<SensorSnapshotResponse>();
        Assert.DoesNotContain(sensor!.MeasuredParameters, p => p.Code == "voc");
        Assert.Contains(sensor.MeasuredParameters, p => p.Code == "co2");
    }

    [Fact]
    public async Task RegisterSensor_WithUnknownStatusCode_Returns400BadRequest()
    {
        var request = new RegisterSensorRequest(
            Manufacturer: "Aranet",
            Model: "Aranet4",
            SerialNumber: "SN-1",
            StatusCode: "flying",
            MeasuredParameters: new[] { "co2" });

        var response = await _client.PostAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterSensor_WithUnknownParameterCode_Returns400BadRequest()
    {
        var request = new RegisterSensorRequest(
            Manufacturer: "Aranet",
            Model: "Aranet4",
            SerialNumber: "SN-1",
            StatusCode: "active",
            MeasuredParameters: new[] { "radiation" });

        var response = await _client.PostAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSensorStatus_WithNonAdvancingValidFrom_Returns400BadRequest()
    {
        var registered = await RegisterSensorAsync();

        var request = new ChangeSensorStatusRequest("maintenance", DateTime.UtcNow.AddYears(-1));
        var response = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/status", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSensorStatus_OnNonexistentSensor_Returns404NotFound()
    {
        var request = new ChangeSensorStatusRequest("maintenance", DateTime.UtcNow.AddHours(1));
        var response = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{Guid.NewGuid()}/status", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSensorById_WithInvalidAsOf_Returns400BadRequest()
    {
        var registered = await RegisterSensorAsync();

        var response = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}?asOf=not-a-timestamp");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- installation details (F08) ----------------------------------------

    [Fact]
    public async Task RegisterSensor_WithInstallation_PersistsAndProjectsIt()
    {
        var request = new RegisterSensorRequest(
            Manufacturer: "Aranet",
            Model: "Aranet4",
            SerialNumber: "SN-0001",
            StatusCode: "active",
            MeasuredParameters: new[] { "co2" },
            Installation: new SensorInstallationRequest(
                PositionNote: "By the north window",
                DistanceWindowM: 1.5,
                DistanceDoorM: 3.0,
                DistanceSourceM: 2.0,
                MeasurementFrequencySeconds: 60,
                InstalledOn: new DateOnly(2026, 5, 1),
                LastCalibratedOn: new DateOnly(2026, 5, 15)));

        var response = await _client.PostAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registered = (await response.Content.ReadFromJsonAsync<SensorSnapshotResponse>())!;

        var get = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        var sensor = (await get.Content.ReadFromJsonAsync<SensorSnapshotResponse>())!;

        Assert.NotNull(sensor.Installation);
        Assert.Equal("By the north window", sensor.Installation!.PositionNote);
        Assert.Equal(1.5, sensor.Installation.DistanceWindowM);
        Assert.Equal(60, sensor.Installation.MeasurementFrequencySeconds);
        Assert.Equal(new DateOnly(2026, 5, 1), sensor.Installation.InstalledOn);
        Assert.Equal(new DateOnly(2026, 5, 15), sensor.Installation.LastCalibratedOn);
    }

    [Fact]
    public async Task RegisterSensor_WithoutInstallation_ProjectsNullInstallation()
    {
        var registered = await RegisterSensorAsync();

        var get = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        var sensor = (await get.Content.ReadFromJsonAsync<SensorSnapshotResponse>())!;

        Assert.Null(sensor.Installation);
    }

    [Fact]
    public async Task ChangeSensorInstallation_OpensRowOnSensorRegisteredWithout()
    {
        var registered = await RegisterSensorAsync();

        var request = new ChangeSensorInstallationRequest(
            PositionNote: "Centre of ceiling",
            DistanceWindowM: 2.0,
            DistanceDoorM: null,
            DistanceSourceM: null,
            MeasurementFrequencySeconds: 30,
            InstalledOn: null,
            LastCalibratedOn: null,
            ValidFrom: DateTime.UtcNow);

        var response = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/installation", request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var get = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}");
        var sensor = (await get.Content.ReadFromJsonAsync<SensorSnapshotResponse>())!;
        Assert.Equal("Centre of ceiling", sensor.Installation!.PositionNote);
        Assert.Equal(30, sensor.Installation.MeasurementFrequencySeconds);
    }

    [Fact]
    public async Task ChangeSensorInstallation_ClosesPreviousRowHalfOpen()
    {
        var registerRequest = new RegisterSensorRequest(
            Manufacturer: "Aranet",
            Model: "Aranet4",
            SerialNumber: "SN-0001",
            StatusCode: "active",
            MeasuredParameters: new[] { "co2" },
            Installation: new SensorInstallationRequest(
                PositionNote: "Original", DistanceWindowM: 1.0, DistanceDoorM: null,
                DistanceSourceM: null, MeasurementFrequencySeconds: null,
                InstalledOn: null, LastCalibratedOn: null));

        var registerResponse = await _client.PostAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors", registerRequest);
        var registered = (await registerResponse.Content.ReadFromJsonAsync<SensorSnapshotResponse>())!;
        var t0 = registered.AsOf;

        // Move the installation forward in time.
        var validFrom = t0.AddHours(1);
        var changeRequest = new ChangeSensorInstallationRequest(
            PositionNote: "Updated", DistanceWindowM: 2.0, DistanceDoorM: null,
            DistanceSourceM: null, MeasurementFrequencySeconds: null,
            InstalledOn: null, LastCalibratedOn: null, ValidFrom: validFrom);

        var changeResponse = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/installation", changeRequest);
        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);

        // asOf before the change still sees the original (half-open close).
        var asOfBefore = Uri.EscapeDataString(validFrom.AddSeconds(-1).ToString("o"));
        var before = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}?asOf={asOfBefore}");
        var beforeSensor = (await before.Content.ReadFromJsonAsync<SensorSnapshotResponse>())!;
        Assert.Equal("Original", beforeSensor.Installation!.PositionNote);

        // asOf after the change sees the new value.
        var asOfAfter = Uri.EscapeDataString(validFrom.AddSeconds(1).ToString("o"));
        var after = await _client.GetAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}?asOf={asOfAfter}");
        var afterSensor = (await after.Content.ReadFromJsonAsync<SensorSnapshotResponse>())!;
        Assert.Equal("Updated", afterSensor.Installation!.PositionNote);
        Assert.Equal(2.0, afterSensor.Installation.DistanceWindowM);
    }

    [Fact]
    public async Task RegisterSensor_WithNonPositiveDistance_Returns400BadRequest()
    {
        var request = new RegisterSensorRequest(
            Manufacturer: "Aranet",
            Model: "Aranet4",
            SerialNumber: "SN-1",
            StatusCode: "active",
            MeasuredParameters: new[] { "co2" },
            Installation: new SensorInstallationRequest(
                PositionNote: null, DistanceWindowM: -1.0, DistanceDoorM: null,
                DistanceSourceM: null, MeasurementFrequencySeconds: null,
                InstalledOn: null, LastCalibratedOn: null));

        var response = await _client.PostAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSensorInstallation_WithCalibrationBeforeInstall_Returns400BadRequest()
    {
        var registered = await RegisterSensorAsync();

        var request = new ChangeSensorInstallationRequest(
            PositionNote: null, DistanceWindowM: null, DistanceDoorM: null,
            DistanceSourceM: null, MeasurementFrequencySeconds: null,
            InstalledOn: new DateOnly(2026, 5, 10),
            LastCalibratedOn: new DateOnly(2026, 5, 1),
            ValidFrom: DateTime.UtcNow);

        var response = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/installation", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSensorInstallation_OnNonexistentSensor_Returns404NotFound()
    {
        var request = new ChangeSensorInstallationRequest(
            PositionNote: "Anywhere", DistanceWindowM: null, DistanceDoorM: null,
            DistanceSourceM: null, MeasurementFrequencySeconds: null,
            InstalledOn: null, LastCalibratedOn: null, ValidFrom: DateTime.UtcNow);

        var response = await _client.PutAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{Guid.NewGuid()}/installation", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMeasuredParameter_OverlappingSameParameter_Returns409Conflict()
    {
        var registered = await RegisterSensorAsync(parameters: new[] { "co2" });

        // co2 already has an open [created, +inf) row; adding it again with an
        // overlapping validity must trip the GiST exclusion constraint.
        var request = new AddMeasuredParameterRequest("co2", DateTime.UtcNow.AddHours(1));
        var response = await _client.PostAsJsonAsync(
            $"/v1/buildings/{_buildingId}/rooms/{_roomId}/sensors/{registered.Id}/measured-parameters", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
