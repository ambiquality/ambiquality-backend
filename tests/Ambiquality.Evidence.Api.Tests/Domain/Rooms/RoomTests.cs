using Ambiquality.Evidence.Api.Domain;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Rooms;

namespace Ambiquality.Evidence.Api.Tests.Domain.Rooms;

public class RoomTests
{
    private static readonly DateTime T0 = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly Guid BuildingId = Guid.NewGuid();

    private static Room RegisterRoom(
        string name = "Office Room 101",
        byte floor = 1,
        string? functionCode = null,
        string? exposureCode = null,
        double? areaM2 = null,
        double? ceilingHeightM = null,
        string? ventilationType = null,
        IReadOnlyCollection<string>? pollutionSources = null)
    {
        return Room.Register(
            slug: UriSlug.Create("office-room-101"),
            buildingId: BuildingId,
            createdBy: Creator,
            name: name,
            floor: FloorNumber.Create(floor),
            functionCode: functionCode,
            exposureCode: exposureCode,
            areaM2: areaM2,
            ceilingHeightM: ceilingHeightM,
            ventilationType: ventilationType,
            pollutionSources: pollutionSources ?? [],
            now: T0);
    }

    [Fact]
    public void Register_SetsIdentityAndAuditFields()
    {
        var room = RegisterRoom();

        Assert.NotEqual(Guid.Empty, room.Id);
        Assert.Equal("office-room-101", room.UriSlug);
        Assert.Equal(BuildingId, room.BuildingId);
        Assert.Equal(Creator, room.CreatedBy);
        Assert.Equal(T0, room.CreatedAt);
    }

    [Fact]
    public void Register_SeedsOpenHistoryRowForEveryAttribute()
    {
        var room = RegisterRoom(
            functionCode: "office",
            exposureCode: "short",
            areaM2: 25.5,
            ceilingHeightM: 2.8,
            ventilationType: "vzt");

        var name = Assert.Single(room.NameHistory);
        Assert.Equal("Office Room 101", name.Name);
        Assert.True(name.Validity.UpperBoundInfinite);

        var floor = Assert.Single(room.FloorHistory);
        Assert.Equal((byte)1, floor.Floor);
        Assert.True(floor.Validity.UpperBoundInfinite);

        var function = Assert.Single(room.FunctionHistory);
        Assert.Equal("office", function.FunctionCode);
        Assert.True(function.Validity.UpperBoundInfinite);

        var exposure = Assert.Single(room.ExposureHistory);
        Assert.Equal("short", exposure.ExposureCode);
        Assert.True(exposure.Validity.UpperBoundInfinite);

        var geometry = Assert.Single(room.GeometryHistory);
        Assert.Equal(25.5, geometry.AreaM2);
        Assert.Equal(2.8, geometry.CeilingHeightM);
        Assert.True(geometry.Validity.UpperBoundInfinite);

        var ventilation = Assert.Single(room.VentilationHistory);
        Assert.Equal("vzt", ventilation.VentilationType);
        Assert.True(ventilation.Validity.UpperBoundInfinite);
    }

    [Fact]
    public void Register_WithOptionalAttributes_SeedsNullRows()
    {
        // Register with minimal attributes
        var room = RegisterRoom(functionCode: null, areaM2: null);

        // Should still have rows for optional attributes, just with null values
        var function = Assert.Single(room.FunctionHistory);
        Assert.Null(function.FunctionCode);

        var geometry = Assert.Single(room.GeometryHistory);
        Assert.Null(geometry.AreaM2);
        Assert.Null(geometry.CeilingHeightM);
    }

    [Fact]
    public void ChangeName_ClosesPreviousAndOpensNewAtValidFrom()
    {
        var room = RegisterRoom();

        room.ChangeName("New Room Name", T1, Creator);

        Assert.Equal(2, room.NameHistory.Count);
        var closed = room.NameHistory.Single(h => !h.Validity.UpperBoundInfinite);
        var open = room.NameHistory.Single(h => h.Validity.UpperBoundInfinite);

        Assert.Equal("Office Room 101", closed.Name);
        Assert.Equal(T0, closed.Validity.LowerBound);
        Assert.Equal(T1, closed.Validity.UpperBound);

        Assert.Equal("New Room Name", open.Name);
        Assert.Equal(T1, open.Validity.LowerBound);
        Assert.True(open.Validity.UpperBoundInfinite);
    }

    [Fact]
    public void ChangeFloor_ClosesPreviousAndOpensNewAtValidFrom()
    {
        var room = RegisterRoom(floor: 1);

        room.ChangeFloor(FloorNumber.Create(2), T1, Creator);

        Assert.Equal(2, room.FloorHistory.Count);
        var closed = room.FloorHistory.Single(h => !h.Validity.UpperBoundInfinite);
        var open = room.FloorHistory.Single(h => h.Validity.UpperBoundInfinite);

        Assert.Equal((byte)1, closed.Floor);
        Assert.Equal((byte)2, open.Floor);
    }

    [Fact]
    public void ChangeFunction_ClosesPreviousAndOpensNew()
    {
        var room = RegisterRoom(functionCode: "office");

        room.ChangeFunction("bedroom", T1, Creator);

        Assert.Equal(2, room.FunctionHistory.Count);
        var closed = room.FunctionHistory.Single(h => !h.Validity.UpperBoundInfinite);
        var open = room.FunctionHistory.Single(h => h.Validity.UpperBoundInfinite);

        Assert.Equal("office", closed.FunctionCode);
        Assert.Equal("bedroom", open.FunctionCode);
    }

    [Fact]
    public void AddPollutionSource_OpensHistoryRowWithValidFrom()
    {
        var room = RegisterRoom();

        room.AddPollutionSource("traffic", T1);

        Assert.Single(room.PollutionSourceHistory);
        var source = room.PollutionSourceHistory.Single();
        Assert.Equal("traffic", source.SourceCode);
        Assert.Equal(T1, source.Validity.LowerBound);
        Assert.True(source.Validity.UpperBoundInfinite);
    }

    [Fact]
    public void AddMultiplePollutionSources_CreatesMultipleRows()
    {
        var room = RegisterRoom();

        room.AddPollutionSource("traffic", T1);
        room.AddPollutionSource("cooking", T1);

        Assert.Equal(2, room.PollutionSourceHistory.Count);
        var codes = room.PollutionSourceHistory.Select(s => s.SourceCode).OrderBy(c => c).ToList();
        Assert.Equal(["cooking", "traffic"], codes);
    }

    [Fact]
    public void RemovePollutionSource_ClosesHistoryRowAtValidTo()
    {
        var room = RegisterRoom();
        room.AddPollutionSource("traffic", T1);

        room.RemovePollutionSource("traffic", T2);

        var source = Assert.Single(room.PollutionSourceHistory);
        Assert.False(source.Validity.UpperBoundInfinite);
        Assert.Equal(T2, source.Validity.UpperBound);
    }

    [Fact]
    public void SnapshotAt_ReturnsCurrentStateAtSpecifiedTime()
    {
        var room = RegisterRoom(name: "Original Name", functionCode: "office");
        room.ChangeName("Updated Name", T1, Creator);

        var snapshotAtT0 = room.SnapshotAt(T0);
        var snapshotAtT1 = room.SnapshotAt(T1.AddSeconds(-1)); // Just before T1
        var snapshotAfterT1 = room.SnapshotAt(T2);

        Assert.Equal("Original Name", snapshotAtT0.Name);
        Assert.Equal("Original Name", snapshotAtT1.Name);
        Assert.Equal("Updated Name", snapshotAfterT1.Name);
    }

    [Fact]
    public void ChangeName_WithValidFromBeforeCurrentOpen_Throws()
    {
        var room = RegisterRoom();

        // Try to change with a time before the current open range started
        var ex = Assert.Throws<DomainException>(() =>
            room.ChangeName("New Name", T0.AddSeconds(-1), Creator));

        Assert.Contains("after", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChangeAttribute_WithValidFromBeforePreviousClosed_Throws()
    {
        var room = RegisterRoom();
        room.ChangeName("First Change", T1, Creator);

        // Try to change with a time before the newly opened range
        var ex = Assert.Throws<DomainException>(() =>
            room.ChangeName("Second Change", T1.AddSeconds(-1), Creator));

        Assert.Contains("after", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChangeName_ClosesPreviousAsHalfOpenRange()
    {
        var room = RegisterRoom();

        room.ChangeName("New Room Name", T1, Creator);

        var closed = room.NameHistory.Single(h => !h.Validity.UpperBoundInfinite);
        var open = room.NameHistory.Single(h => h.Validity.UpperBoundInfinite);

        // Half-open [T0, T1): the closed row's upper bound must be exclusive so it
        // does not share the boundary instant T1 with the next open row [T1, +inf).
        // An inclusive upper bound here would overlap and trip the GiST constraint.
        Assert.True(closed.Validity.LowerBoundIsInclusive);
        Assert.False(closed.Validity.UpperBoundIsInclusive);
        Assert.Equal(T1, closed.Validity.UpperBound);
        Assert.Equal(T1, open.Validity.LowerBound);
    }

    [Fact]
    public void RemovePollutionSource_WithValidToBeforeOpen_Throws()
    {
        var room = RegisterRoom();
        room.AddPollutionSource("traffic", T1);

        var ex = Assert.Throws<DomainException>(() =>
            room.RemovePollutionSource("traffic", T1.AddSeconds(-1)));

        Assert.Contains("after", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- idempotent re-PUT (exact replay) ----------------------------------

    [Fact]
    public void ChangeName_ExactReplay_IsNoOp()
    {
        var room = RegisterRoom(name: "Office Room 101");

        room.ChangeName("Office Room 101", T0, Creator);

        var open = Assert.Single(room.NameHistory);
        Assert.True(open.Validity.UpperBoundInfinite);
        Assert.Equal("Office Room 101", open.Name);
    }

    [Fact]
    public void ChangeName_SameValidFromDifferentValue_StillThrows()
    {
        var room = RegisterRoom(name: "Office Room 101");

        Assert.Throws<DomainException>(() => room.ChangeName("Different", T0, Creator));
    }

    [Fact]
    public void ChangeName_SameValueLaterValidFrom_StillAppends()
    {
        var room = RegisterRoom(name: "Office Room 101");

        room.ChangeName("Office Room 101", T1, Creator);

        Assert.Equal(2, room.NameHistory.Count);
    }

    [Fact]
    public void ChangeFloor_ExactReplay_IsNoOp()
    {
        var room = RegisterRoom(floor: 1);

        // FloorNumber.Value (1) matches the open row's .Floor.
        room.ChangeFloor(FloorNumber.Create(1), T0, Creator);

        Assert.Single(room.FloorHistory);
    }

    [Fact]
    public void ChangeFloor_SameValidFromDifferentValue_StillThrows()
    {
        var room = RegisterRoom(floor: 1);

        Assert.Throws<DomainException>(() => room.ChangeFloor(FloorNumber.Create(2), T0, Creator));
    }

    [Fact]
    public void ChangeFunction_ExactReplay_IsNoOp()
    {
        var room = RegisterRoom(functionCode: "office");

        room.ChangeFunction("office", T0, Creator);

        Assert.Single(room.FunctionHistory);
    }

    [Fact]
    public void ChangeFunction_ExactReplayWithNull_IsNoOp()
    {
        var room = RegisterRoom(functionCode: null);

        room.ChangeFunction(null, T0, Creator);

        Assert.Single(room.FunctionHistory);
    }

    [Fact]
    public void ChangeExposure_ExactReplay_IsNoOp()
    {
        var room = RegisterRoom(exposureCode: "short");

        room.ChangeExposure("short", T0, Creator);

        Assert.Single(room.ExposureHistory);
    }

    [Fact]
    public void ChangeGeometry_ExactReplay_IsNoOp()
    {
        var room = RegisterRoom(areaM2: 25.5, ceilingHeightM: 2.8);

        room.ChangeGeometry(25.5, 2.8, T0, Creator);

        Assert.Single(room.GeometryHistory);
    }

    [Fact]
    public void ChangeGeometry_SameValidFromDifferentValue_StillThrows()
    {
        var room = RegisterRoom(areaM2: 25.5, ceilingHeightM: 2.8);

        // CeilingHeightM differs at the same validFrom.
        Assert.Throws<DomainException>(() => room.ChangeGeometry(25.5, 3.0, T0, Creator));
    }

    [Fact]
    public void ChangeGeometry_SameValueLaterValidFrom_StillAppends()
    {
        var room = RegisterRoom(areaM2: 25.5, ceilingHeightM: 2.8);

        room.ChangeGeometry(25.5, 2.8, T1, Creator);

        Assert.Equal(2, room.GeometryHistory.Count);
    }

    [Fact]
    public void ChangeVentilation_ExactReplay_IsNoOp()
    {
        var room = RegisterRoom(ventilationType: "vzt");

        room.ChangeVentilation("vzt", T0, Creator);

        Assert.Single(room.VentilationHistory);
    }
}

