using Ambiquality.Evidence.Api.Application;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Tests.TestSupport;

namespace Ambiquality.Evidence.Api.Tests.Application.Buildings;

public class ChangeBuildingHandlersTests
{
    private static readonly DateTime T0 = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid OtherUser = Guid.NewGuid();
    private static readonly Address Addr =
        Address.Create("Náměstí 1", "Praha", "11000", "CZ");

    private readonly InMemoryBuildingRepository _repository = new();
    private readonly FakeClock _clock = new(T1);
    private readonly StubCurrentUser _owner = new(Guid.NewGuid(), Owner);
    private readonly StubCurrentUser _intruder = new(Guid.NewGuid(), OtherUser);

    private Building SeedBuilding()
    {
        var building = Building.Register(
            UriSlug.Create("praha-budova-01"),
            ownerId: Owner,
            createdBy: Owner,
            name: "Original",
            address: Addr,
            buildingTypeCode: "office",
            coordinates: Coordinates.Create(50.0, 14.0),
            anonymization: AnonymizationLevel.Precise,
            yearBuilt: 1990,
            yearRenovated: null,
            now: T0);
        _repository.Add(building);
        return building;
    }

    [Fact]
    public async Task ChangeName_AsOwner_ClosesPreviousAndAppendsNew()
    {
        var building = SeedBuilding();
        var handler = new ChangeBuildingNameHandler(_repository, _owner);

        await handler.HandleAsync(new ChangeBuildingNameCommand(building.Id, "Renamed", T1));

        Assert.Equal(2, building.NameHistory.Count);
        Assert.Equal(1, _repository.SaveChangesCallCount);
        Assert.Equal("Renamed", building.NameHistory.Single(h => h.Validity.UpperBoundInfinite).Name);
    }

    [Fact]
    public async Task ChangeName_AsNonOwner_ThrowsForbidden()
    {
        var building = SeedBuilding();
        var handler = new ChangeBuildingNameHandler(_repository, _intruder);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(new ChangeBuildingNameCommand(building.Id, "Hack", T1)));
        Assert.Single(building.NameHistory);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task ChangeName_UnknownBuilding_ThrowsNotFound()
    {
        var handler = new ChangeBuildingNameHandler(_repository, _owner);

        await Assert.ThrowsAsync<BuildingNotFoundException>(() =>
            handler.HandleAsync(new ChangeBuildingNameCommand(Guid.NewGuid(), "X", T1)));
    }

    [Fact]
    public async Task ChangeAddress_AsOwner_AppendsNew()
    {
        var building = SeedBuilding();
        var handler = new ChangeBuildingAddressHandler(_repository, _owner);
        var newAddr = Address.Create("Jiná 2", "Brno", "60200", "CZ");

        await handler.HandleAsync(new ChangeBuildingAddressCommand(
            building.Id, newAddr.Street, newAddr.City, newAddr.Postcode, newAddr.Country, T1));

        Assert.Equal(2, building.AddressHistory.Count);
        Assert.Equal(newAddr, building.AddressHistory.Single(h => h.Validity.UpperBoundInfinite).Address);
    }

    [Fact]
    public async Task ChangeType_AsOwner_AppendsNew()
    {
        var building = SeedBuilding();
        var handler = new ChangeBuildingTypeHandler(_repository, _owner);

        await handler.HandleAsync(new ChangeBuildingTypeCommand(building.Id, "educational", T1));

        Assert.Equal("educational", building.TypeHistory.Single(h => h.Validity.UpperBoundInfinite).BuildingTypeCode);
    }

    [Fact]
    public async Task ChangeLocation_AsOwner_AppendsNullableCoords()
    {
        var building = SeedBuilding();
        var handler = new ChangeBuildingLocationHandler(_repository, _owner);

        await handler.HandleAsync(new ChangeBuildingLocationCommand(
            building.Id, Latitude: null, Longitude: null, AnonymizationLevel: "municipality", T1));

        var open = building.LocationHistory.Single(h => h.Validity.UpperBoundInfinite);
        Assert.Null(open.Coordinates);
        Assert.Equal(AnonymizationLevel.Municipality, open.Anonymization);
    }

    [Fact]
    public async Task ChangeLocation_WithUnknownLevel_Throws()
    {
        var building = SeedBuilding();
        var handler = new ChangeBuildingLocationHandler(_repository, _owner);

        await Assert.ThrowsAsync<UnknownCodelistCodeException>(() =>
            handler.HandleAsync(new ChangeBuildingLocationCommand(
                building.Id, 50.0, 14.0, "city", T1)));
    }

    [Fact]
    public async Task ChangeYears_AsOwner_AppendsNew()
    {
        var building = SeedBuilding();
        var handler = new ChangeBuildingYearsHandler(_repository, _owner);

        await handler.HandleAsync(new ChangeBuildingYearsCommand(building.Id, 1990, 2020, T1));

        var open = building.YearsHistory.Single(h => h.Validity.UpperBoundInfinite);
        Assert.Equal((short)1990, open.YearBuilt);
        Assert.Equal((short)2020, open.YearRenovated);
    }

    [Fact]
    public async Task EveryChangeHandler_AsNonOwner_ThrowsForbidden()
    {
        var building = SeedBuilding();
        await Assert.ThrowsAsync<ForbiddenException>(() => new ChangeBuildingAddressHandler(_repository, _intruder)
            .HandleAsync(new ChangeBuildingAddressCommand(building.Id, "S", "P", "11000", "CZ", T1)));
        await Assert.ThrowsAsync<ForbiddenException>(() => new ChangeBuildingTypeHandler(_repository, _intruder)
            .HandleAsync(new ChangeBuildingTypeCommand(building.Id, "educational", T1)));
        await Assert.ThrowsAsync<ForbiddenException>(() => new ChangeBuildingLocationHandler(_repository, _intruder)
            .HandleAsync(new ChangeBuildingLocationCommand(building.Id, null, null, "municipality", T1)));
        await Assert.ThrowsAsync<ForbiddenException>(() => new ChangeBuildingYearsHandler(_repository, _intruder)
            .HandleAsync(new ChangeBuildingYearsCommand(building.Id, 1990, 2020, T1)));
    }
}
