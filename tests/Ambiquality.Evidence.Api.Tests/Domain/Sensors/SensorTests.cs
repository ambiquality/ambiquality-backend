using Ambiquality.Evidence.Api.Domain;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Tests.Domain.Sensors;

public class SensorTests
{
    private static readonly DateTime T0 = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly Guid BuildingId = Guid.NewGuid();
    private static readonly Guid RoomId = Guid.NewGuid();

    private static Sensor RegisterSensor(
        string manufacturer = "Aranet",
        string model = "Aranet4",
        string serialNumber = "SN-0001",
        SensorStatus? status = null,
        IReadOnlyCollection<MeasuredParameter>? parameters = null,
        string apiKeyHash = "test-hash")
    {
        return Sensor.Register(
            slug: UriSlug.Create("aranet4-0001"),
            buildingId: BuildingId,
            roomId: RoomId,
            createdBy: Creator,
            manufacturer: manufacturer,
            model: model,
            serialNumber: serialNumber,
            status: status ?? SensorStatus.Active,
            measuredParameters: parameters ?? [MeasuredParameter.Co2, MeasuredParameter.Temperature],
            apiKeyHash: apiKeyHash,
            now: T0);
    }

    [Fact]
    public void Register_SetsIdentityAndAuditFields()
    {
        var sensor = RegisterSensor();

        Assert.NotEqual(Guid.Empty, sensor.Id);
        Assert.Equal("aranet4-0001", sensor.UriSlug);
        Assert.Equal(BuildingId, sensor.CurrentBuildingId);
        Assert.Equal(RoomId, sensor.CurrentRoomId);
        Assert.Equal(Creator, sensor.CreatedBy);
        Assert.Equal(T0, sensor.CreatedAt);
    }

    [Fact]
    public void Register_StoresApiKeyHash()
    {
        var sensor = RegisterSensor(apiKeyHash: "deadbeef");

        Assert.Equal("deadbeef", sensor.ApiKeyHash);
    }

    [Fact]
    public void Register_SeedsOpenHistoryRowForEveryAttribute()
    {
        var sensor = RegisterSensor();

        var identity = Assert.Single(sensor.IdentityHistory);
        Assert.Equal("Aranet", identity.Manufacturer);
        Assert.Equal("Aranet4", identity.Model);
        Assert.Equal("SN-0001", identity.SerialNumber);
        Assert.True(identity.Validity.UpperBoundInfinite);

        var placement = Assert.Single(sensor.PlacementHistory);
        Assert.Equal(BuildingId, placement.BuildingId);
        Assert.Equal(RoomId, placement.RoomId);
        Assert.True(placement.Validity.UpperBoundInfinite);

        var status = Assert.Single(sensor.StatusHistory);
        Assert.Equal("active", status.StatusCode);
        Assert.True(status.Validity.UpperBoundInfinite);

        Assert.Equal(2, sensor.MeasuredParameterHistory.Count);
        Assert.All(sensor.MeasuredParameterHistory, h => Assert.True(h.Validity.UpperBoundInfinite));
    }

    [Fact]
    public void ChangeIdentity_ClosesPreviousAndOpensNewAtValidFrom()
    {
        var sensor = RegisterSensor();

        sensor.ChangeIdentity("Aranet", "Aranet4 Pro", "SN-0002", T1, Creator);

        Assert.Equal(2, sensor.IdentityHistory.Count);
        var closed = sensor.IdentityHistory.Single(h => !h.Validity.UpperBoundInfinite);
        var open = sensor.IdentityHistory.Single(h => h.Validity.UpperBoundInfinite);

        Assert.Equal("SN-0001", closed.SerialNumber);
        Assert.Equal(T1, closed.Validity.UpperBound);
        Assert.Equal("Aranet4 Pro", open.Model);
        Assert.Equal("SN-0002", open.SerialNumber);
    }

    [Fact]
    public void ChangePlacement_ClosesPreviousAndUpdatesDenormalisedPointers()
    {
        var sensor = RegisterSensor();
        var newBuilding = Guid.NewGuid();
        var newRoom = Guid.NewGuid();

        sensor.ChangePlacement(newBuilding, newRoom, T1, Creator);

        Assert.Equal(2, sensor.PlacementHistory.Count);
        var open = sensor.PlacementHistory.Single(h => h.Validity.UpperBoundInfinite);
        Assert.Equal(newBuilding, open.BuildingId);
        Assert.Equal(newRoom, open.RoomId);
        Assert.Equal(newBuilding, sensor.CurrentBuildingId);
        Assert.Equal(newRoom, sensor.CurrentRoomId);
    }

    [Fact]
    public void ChangeStatus_ClosesPreviousAndOpensNew()
    {
        var sensor = RegisterSensor();

        sensor.ChangeStatus(SensorStatus.Maintenance, T1, Creator);

        Assert.Equal(2, sensor.StatusHistory.Count);
        var closed = sensor.StatusHistory.Single(h => !h.Validity.UpperBoundInfinite);
        var open = sensor.StatusHistory.Single(h => h.Validity.UpperBoundInfinite);
        Assert.Equal("active", closed.StatusCode);
        Assert.Equal("maintenance", open.StatusCode);
    }

    [Fact]
    public void AddMeasuredParameter_OpensHistoryRowWithValidFrom()
    {
        var sensor = RegisterSensor(parameters: [MeasuredParameter.Co2]);

        sensor.AddMeasuredParameter(MeasuredParameter.Humidity, T1, Creator);

        var added = sensor.MeasuredParameterHistory.Single(h => h.ParameterCode == "humidity");
        Assert.Equal(T1, added.Validity.LowerBound);
        Assert.True(added.Validity.UpperBoundInfinite);
    }

    [Fact]
    public void RemoveMeasuredParameter_ClosesHistoryRowAtValidTo()
    {
        var sensor = RegisterSensor(parameters: [MeasuredParameter.Co2]);

        sensor.RemoveMeasuredParameter("co2", T2);

        var row = Assert.Single(sensor.MeasuredParameterHistory);
        Assert.False(row.Validity.UpperBoundInfinite);
        Assert.Equal(T2, row.Validity.UpperBound);
    }

    [Fact]
    public void RemoveMeasuredParameter_WhenNotPresent_Throws()
    {
        var sensor = RegisterSensor(parameters: [MeasuredParameter.Co2]);

        Assert.Throws<MeasuredParameterNotFoundException>(() =>
            sensor.RemoveMeasuredParameter("illuminance", T2));
    }

    [Fact]
    public void SnapshotAt_ReturnsStateAtSpecifiedTime()
    {
        var sensor = RegisterSensor(status: SensorStatus.Active);
        sensor.ChangeStatus(SensorStatus.Decommissioned, T1, Creator);

        Assert.Equal("active", sensor.SnapshotAt(T0).StatusCode);
        Assert.Equal("active", sensor.SnapshotAt(T1.AddSeconds(-1)).StatusCode);
        Assert.Equal("decommissioned", sensor.SnapshotAt(T2).StatusCode);
    }

    [Fact]
    public void ChangeStatus_WithValidFromBeforeCurrentOpen_Throws()
    {
        var sensor = RegisterSensor();

        var ex = Assert.Throws<DomainException>(() =>
            sensor.ChangeStatus(SensorStatus.Maintenance, T0.AddSeconds(-1), Creator));

        Assert.Contains("after", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromCode_UnknownStatus_Throws()
    {
        Assert.Throws<ArgumentException>(() => SensorStatus.FromCode("flying"));
    }

    [Fact]
    public void FromCode_UnknownParameter_Throws()
    {
        Assert.Throws<ArgumentException>(() => MeasuredParameter.FromCode("radiation"));
    }

    // ---- idempotent re-PUT (exact replay) ----------------------------------

    [Fact]
    public void ChangeIdentity_ExactReplay_IsNoOp()
    {
        var sensor = RegisterSensor(manufacturer: "Aranet", model: "Aranet4", serialNumber: "SN-0001");

        sensor.ChangeIdentity("Aranet", "Aranet4", "SN-0001", T0, Creator);

        var open = Assert.Single(sensor.IdentityHistory);
        Assert.True(open.Validity.UpperBoundInfinite);
        Assert.Equal("SN-0001", open.SerialNumber);
    }

    [Fact]
    public void ChangeIdentity_SameValidFromDifferentValue_StillThrows()
    {
        var sensor = RegisterSensor(serialNumber: "SN-0001");

        // SerialNumber differs at the same validFrom.
        Assert.Throws<DomainException>(() =>
            sensor.ChangeIdentity("Aranet", "Aranet4", "SN-9999", T0, Creator));
    }

    [Fact]
    public void ChangeIdentity_SameValueLaterValidFrom_StillAppends()
    {
        var sensor = RegisterSensor(manufacturer: "Aranet", model: "Aranet4", serialNumber: "SN-0001");

        sensor.ChangeIdentity("Aranet", "Aranet4", "SN-0001", T1, Creator);

        Assert.Equal(2, sensor.IdentityHistory.Count);
    }

    [Fact]
    public void ChangePlacement_ExactReplay_IsNoOp()
    {
        var sensor = RegisterSensor();

        // BuildingId + RoomId match the open placement row.
        sensor.ChangePlacement(BuildingId, RoomId, T0, Creator);

        Assert.Single(sensor.PlacementHistory);
        // The denormalised pointers must remain at the original placement.
        Assert.Equal(BuildingId, sensor.CurrentBuildingId);
        Assert.Equal(RoomId, sensor.CurrentRoomId);
    }

    [Fact]
    public void ChangePlacement_SameValidFromDifferentValue_StillThrows()
    {
        var sensor = RegisterSensor();
        var newRoom = Guid.NewGuid();

        Assert.Throws<DomainException>(() =>
            sensor.ChangePlacement(BuildingId, newRoom, T0, Creator));
    }

    [Fact]
    public void ChangeStatus_ExactReplay_IsNoOp()
    {
        var sensor = RegisterSensor(status: SensorStatus.Active);

        // SensorStatus.Code ("active") matches the open row's .StatusCode.
        sensor.ChangeStatus(SensorStatus.Active, T0, Creator);

        var open = Assert.Single(sensor.StatusHistory);
        Assert.Equal("active", open.StatusCode);
    }

    [Fact]
    public void ChangeStatus_SameValidFromDifferentValue_StillThrows()
    {
        var sensor = RegisterSensor(status: SensorStatus.Active);

        Assert.Throws<DomainException>(() =>
            sensor.ChangeStatus(SensorStatus.Maintenance, T0, Creator));
    }

    [Fact]
    public void ChangeStatus_SameValueLaterValidFrom_StillAppends()
    {
        var sensor = RegisterSensor(status: SensorStatus.Active);

        sensor.ChangeStatus(SensorStatus.Active, T1, Creator);

        Assert.Equal(2, sensor.StatusHistory.Count);
    }

    // ---- Close() delegates to the Validity factory's guards -----------------
    // Each sensor history row's Close() must route through Common.Validity.Closed
    // (the sole legal range factory) so a non-UTC upper bound is rejected with the
    // same ArgumentException Building/Room rows raise, instead of silently building
    // a raw NpgsqlRange.

    private static readonly DateTime NonUtc =
        new(2026, 6, 1, 12, 0, 0, DateTimeKind.Local);

    [Fact]
    public void SensorIdentityHistory_Close_WithNonUtcValidFrom_Throws()
    {
        var row = new SensorIdentityHistory(
            sensorId: Guid.NewGuid(),
            validity: Validity.OpenFrom(T0),
            manufacturer: "Aranet",
            model: "Aranet4",
            serialNumber: "SN-0001",
            recordedBy: Creator,
            recordedAt: T0);

        Assert.Throws<ArgumentException>(() => row.Close(NonUtc));
    }

    [Fact]
    public void SensorStatusHistory_Close_WithNonUtcValidFrom_Throws()
    {
        var row = new SensorStatusHistory(
            sensorId: Guid.NewGuid(),
            validity: Validity.OpenFrom(T0),
            statusCode: "active",
            recordedBy: Creator,
            recordedAt: T0);

        Assert.Throws<ArgumentException>(() => row.Close(NonUtc));
    }

    [Fact]
    public void SensorPlacementHistory_Close_WithNonUtcValidFrom_Throws()
    {
        var row = new SensorPlacementHistory(
            sensorId: Guid.NewGuid(),
            validity: Validity.OpenFrom(T0),
            buildingId: BuildingId,
            roomId: RoomId,
            recordedBy: Creator,
            recordedAt: T0);

        Assert.Throws<ArgumentException>(() => row.Close(NonUtc));
    }

    [Fact]
    public void SensorMeasuredParameterHistory_Close_WithNonUtcValidFrom_Throws()
    {
        var row = new SensorMeasuredParameterHistory(
            sensorId: Guid.NewGuid(),
            parameterCode: "co2",
            validity: Validity.OpenFrom(T0),
            recordedBy: Creator,
            recordedAt: T0);

        Assert.Throws<ArgumentException>(() => row.Close(NonUtc));
    }

    [Fact]
    public void SensorIdentityHistory_Close_WithValidUtcValidFrom_ClosesHalfOpen()
    {
        var row = new SensorIdentityHistory(
            sensorId: Guid.NewGuid(),
            validity: Validity.OpenFrom(T0),
            manufacturer: "Aranet",
            model: "Aranet4",
            serialNumber: "SN-0001",
            recordedBy: Creator,
            recordedAt: T0);

        row.Close(T1);

        // Behaviour for valid input is unchanged: [T0, T1) — exclusive upper.
        Assert.Equal(T0, row.Validity.LowerBound);
        Assert.True(row.Validity.LowerBoundIsInclusive);
        Assert.Equal(T1, row.Validity.UpperBound);
        Assert.False(row.Validity.UpperBoundIsInclusive);
        Assert.False(row.Validity.UpperBoundInfinite);
    }
}

