using Ambiquality.Evidence.Api.Domain;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Tests.Domain.Buildings;

public class BuildingTests
{
    private static readonly DateTime T0 = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly Address SampleAddress =
        Address.Create("Náměstí 1", "Praha", "11000", "CZ");
    private static readonly Coordinates SampleCoords =
        Coordinates.Create(50.087, 14.421);

    private static Building RegisterBuilding(
        Coordinates? coordinates = null,
        AnonymizationLevel? anonymization = null,
        short? yearBuilt = 1990,
        short? yearRenovated = null)
    {
        return Building.Register(
            slug: UriSlug.Create("praha-budova-01"),
            ownerId: Owner,
            createdBy: Creator,
            name: "Sídlo VŠE",
            address: SampleAddress,
            buildingTypeCode: "office",
            coordinates: coordinates,
            anonymization: anonymization ?? AnonymizationLevel.Precise,
            yearBuilt: yearBuilt,
            yearRenovated: yearRenovated,
            now: T0);
    }

    [Fact]
    public void Register_SetsIdentityAndAuditFields()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        Assert.NotEqual(Guid.Empty, building.Id);
        Assert.Equal("praha-budova-01", building.UriSlug);
        Assert.Equal(Owner, building.OwnerId);
        Assert.Equal(Creator, building.CreatedBy);
        Assert.Equal(T0, building.CreatedAt);
    }

    [Fact]
    public void Register_SeedsOpenHistoryRowForEveryAttribute()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        var name = Assert.Single(building.NameHistory);
        Assert.Equal("Sídlo VŠE", name.Name);
        Assert.True(name.Validity.UpperBoundInfinite);

        var address = Assert.Single(building.AddressHistory);
        Assert.Equal(SampleAddress, address.Address);
        Assert.True(address.Validity.UpperBoundInfinite);

        var type = Assert.Single(building.TypeHistory);
        Assert.Equal("office", type.BuildingTypeCode);
        Assert.True(type.Validity.UpperBoundInfinite);

        var location = Assert.Single(building.LocationHistory);
        Assert.Equal(SampleCoords, location.Coordinates);
        Assert.Equal(AnonymizationLevel.Precise, location.Anonymization);
        Assert.True(location.Validity.UpperBoundInfinite);

        var years = Assert.Single(building.YearsHistory);
        Assert.Equal((short)1990, years.YearBuilt);
        Assert.Null(years.YearRenovated);
        Assert.True(years.Validity.UpperBoundInfinite);
    }

    [Fact]
    public void Register_WithNullCoordinates_StillSeedsLocationRowForLevel()
    {
        var building = RegisterBuilding(
            coordinates: null,
            anonymization: AnonymizationLevel.Municipality);

        var location = Assert.Single(building.LocationHistory);
        Assert.Null(location.Coordinates);
        Assert.Equal(AnonymizationLevel.Municipality, location.Anonymization);
    }

    [Fact]
    public void ChangeName_ClosesPreviousAndOpensNewAtValidFrom()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        building.ChangeName("New Name", T1, Creator);

        Assert.Equal(2, building.NameHistory.Count);
        var closed = building.NameHistory.Single(h => !h.Validity.UpperBoundInfinite);
        var open = building.NameHistory.Single(h => h.Validity.UpperBoundInfinite);

        Assert.Equal("Sídlo VŠE", closed.Name);
        Assert.Equal(T0, closed.Validity.LowerBound);
        Assert.Equal(T1, closed.Validity.UpperBound);

        Assert.Equal("New Name", open.Name);
        Assert.Equal(T1, open.Validity.LowerBound);
    }

    [Fact]
    public void ChangeName_ThreeVersions_KeepsAllRangesContiguous()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        building.ChangeName("Second", T1, Creator);
        building.ChangeName("Third", T2, Creator);

        Assert.Equal(3, building.NameHistory.Count);
        var ordered = building.NameHistory.OrderBy(h => h.Validity.LowerBound).ToList();
        Assert.Equal("Sídlo VŠE", ordered[0].Name);
        Assert.Equal(T0, ordered[0].Validity.LowerBound);
        Assert.Equal(T1, ordered[0].Validity.UpperBound);
        Assert.Equal("Second", ordered[1].Name);
        Assert.Equal(T1, ordered[1].Validity.LowerBound);
        Assert.Equal(T2, ordered[1].Validity.UpperBound);
        Assert.Equal("Third", ordered[2].Name);
        Assert.Equal(T2, ordered[2].Validity.LowerBound);
        Assert.True(ordered[2].Validity.UpperBoundInfinite);
    }

    [Fact]
    public void ChangeName_WithValidFromAtOrBeforeOpen_Throws()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        Assert.Throws<DomainException>(() => building.ChangeName("X", T0, Creator));
        Assert.Throws<DomainException>(() =>
            building.ChangeName("X", T0.AddSeconds(-1), Creator));
    }

    [Fact]
    public void ChangeName_WithEmptyName_Throws()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);
        Assert.Throws<DomainException>(() => building.ChangeName("", T1, Creator));
        Assert.Throws<DomainException>(() => building.ChangeName("   ", T1, Creator));
    }

    [Fact]
    public void ChangeAddress_ClosesPreviousAndOpensNew()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);
        var newAddress = Address.Create("Jiná 2", "Brno", "60200", "CZ");

        building.ChangeAddress(newAddress, T1, Creator);

        Assert.Equal(2, building.AddressHistory.Count);
        var open = building.AddressHistory.Single(h => h.Validity.UpperBoundInfinite);
        Assert.Equal(newAddress, open.Address);
        Assert.Equal(T1, open.Validity.LowerBound);
    }

    [Fact]
    public void ChangeType_ClosesPreviousAndOpensNew()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        building.ChangeType("school", T1, Creator);

        Assert.Equal(2, building.TypeHistory.Count);
        var open = building.TypeHistory.Single(h => h.Validity.UpperBoundInfinite);
        Assert.Equal("school", open.BuildingTypeCode);
    }

    [Fact]
    public void ChangeLocation_ClosesPreviousAndOpensNewWithNullableCoords()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        building.ChangeLocation(null, AnonymizationLevel.Municipality, T1, Creator);

        Assert.Equal(2, building.LocationHistory.Count);
        var open = building.LocationHistory.Single(h => h.Validity.UpperBoundInfinite);
        Assert.Null(open.Coordinates);
        Assert.Equal(AnonymizationLevel.Municipality, open.Anonymization);
    }

    [Fact]
    public void ChangeYears_ClosesPreviousAndOpensNew()
    {
        var building = RegisterBuilding(yearBuilt: 1990, yearRenovated: null);

        building.ChangeYears(1990, 2020, T1, Creator);

        Assert.Equal(2, building.YearsHistory.Count);
        var open = building.YearsHistory.Single(h => h.Validity.UpperBoundInfinite);
        Assert.Equal((short)1990, open.YearBuilt);
        Assert.Equal((short)2020, open.YearRenovated);
    }

    [Fact]
    public void SnapshotAt_ReturnsValuesValidAtTheGivenInstant()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);
        building.ChangeName("Second", T1, Creator);
        building.ChangeName("Third", T2, Creator);

        var atT0 = building.SnapshotAt(T0);
        var atBetween = building.SnapshotAt(T1.AddDays(15));
        var atT2 = building.SnapshotAt(T2);

        Assert.Equal("Sídlo VŠE", atT0.Name);
        Assert.Equal("Second", atBetween.Name);
        Assert.Equal("Third", atT2.Name);
        Assert.Equal(building.Id, atT2.Id);
        Assert.Equal(building.OwnerId, atT2.OwnerId);
        Assert.Equal(SampleAddress, atT2.Address);
        Assert.Equal("office", atT2.BuildingTypeCode);
        Assert.Equal(SampleCoords, atT2.Coordinates);
        Assert.Equal(AnonymizationLevel.Precise, atT2.Anonymization);
        Assert.Equal((short)1990, atT2.YearBuilt);
        Assert.Equal(T2, atT2.AsOf);
    }

    [Fact]
    public void SnapshotAt_BeforeCreation_Throws()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);
        Assert.Throws<DomainException>(() => building.SnapshotAt(T0.AddDays(-1)));
    }

    // ---- idempotent re-PUT (exact replay) ----------------------------------
    // An identical re-PUT - same value AND same validFrom as the current open
    // row - is a silent no-op: no new history row, no throw. A same-validFrom
    // change with a DIFFERENT value still throws; a same-value change at a
    // strictly later validFrom still appends a row.

    [Fact]
    public void ChangeName_ExactReplay_IsNoOp()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        // Same value ("Sídlo VŠE") at the same instant the open row starts (T0).
        building.ChangeName("Sídlo VŠE", T0, Creator);

        var open = Assert.Single(building.NameHistory);
        Assert.True(open.Validity.UpperBoundInfinite);
        Assert.Equal("Sídlo VŠE", open.Name);
    }

    [Fact]
    public void ChangeName_SameValidFromDifferentValue_StillThrows()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        Assert.Throws<DomainException>(() => building.ChangeName("Different", T0, Creator));
    }

    [Fact]
    public void ChangeName_SameValueLaterValidFrom_StillAppends()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        building.ChangeName("Sídlo VŠE", T1, Creator);

        Assert.Equal(2, building.NameHistory.Count);
    }

    [Fact]
    public void ChangeAddress_ExactReplay_IsNoOp()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        building.ChangeAddress(SampleAddress, T0, Creator);

        Assert.Single(building.AddressHistory);
    }

    [Fact]
    public void ChangeAddress_SameValidFromDifferentValue_StillThrows()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);
        var other = Address.Create("Jiná 2", "Brno", "60200", "CZ");

        Assert.Throws<DomainException>(() => building.ChangeAddress(other, T0, Creator));
    }

    [Fact]
    public void ChangeType_ExactReplay_IsNoOp()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        building.ChangeType("office", T0, Creator);

        Assert.Single(building.TypeHistory);
    }

    [Fact]
    public void ChangeLocation_ExactReplay_IsNoOp()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        // Coordinates (record) + Anonymization (IEquatable) both match the open row.
        building.ChangeLocation(SampleCoords, AnonymizationLevel.Precise, T0, Creator);

        Assert.Single(building.LocationHistory);
    }

    [Fact]
    public void ChangeLocation_ExactReplayWithNullCoordinates_IsNoOp()
    {
        var building = RegisterBuilding(
            coordinates: null,
            anonymization: AnonymizationLevel.Municipality);

        building.ChangeLocation(null, AnonymizationLevel.Municipality, T0, Creator);

        Assert.Single(building.LocationHistory);
    }

    [Fact]
    public void ChangeLocation_SameValidFromDifferentAnonymization_StillThrows()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);

        // Same coordinates but a different anonymization level is NOT an exact replay.
        Assert.Throws<DomainException>(() =>
            building.ChangeLocation(SampleCoords, AnonymizationLevel.Street, T0, Creator));
    }

    [Fact]
    public void ChangeLocation_SameValidFromDifferentCoordinates_StillThrows()
    {
        var building = RegisterBuilding(coordinates: SampleCoords);
        var moved = Coordinates.Create(48.0, 16.0);

        Assert.Throws<DomainException>(() =>
            building.ChangeLocation(moved, AnonymizationLevel.Precise, T0, Creator));
    }

    [Fact]
    public void ChangeYears_ExactReplay_IsNoOp()
    {
        var building = RegisterBuilding(yearBuilt: 1990, yearRenovated: null);

        building.ChangeYears(1990, null, T0, Creator);

        Assert.Single(building.YearsHistory);
    }

    [Fact]
    public void ChangeYears_SameValidFromDifferentValue_StillThrows()
    {
        var building = RegisterBuilding(yearBuilt: 1990, yearRenovated: null);

        // YearRenovated differs (null -> 2020) at the same validFrom.
        Assert.Throws<DomainException>(() => building.ChangeYears(1990, 2020, T0, Creator));
    }

    [Fact]
    public void ChangeYears_SameValueLaterValidFrom_StillAppends()
    {
        var building = RegisterBuilding(yearBuilt: 1990, yearRenovated: null);

        building.ChangeYears(1990, null, T1, Creator);

        Assert.Equal(2, building.YearsHistory.Count);
    }
}

