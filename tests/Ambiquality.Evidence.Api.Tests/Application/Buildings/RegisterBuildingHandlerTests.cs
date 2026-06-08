using Ambiquality.Evidence.Api.Application;
using Ambiquality.Evidence.Api.Application.Buildings;
using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Tests.TestSupport;

namespace Ambiquality.Evidence.Api.Tests.Application.Buildings;

public class RegisterBuildingHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AuthUserId = Guid.NewGuid();
    private static readonly Guid ProjectionId = Guid.NewGuid();

    private readonly InMemoryBuildingRepository _repository = new();
    private readonly FakeClock _clock = new(Now);
    private readonly StubCurrentUser _currentUser = new(AuthUserId, ProjectionId);
    private readonly StubSlugGenerator _slugGenerator = new();

    private RegisterBuildingHandler CreateHandler() =>
        new(_repository, _clock, _currentUser, _slugGenerator);

    private static RegisterBuildingCommand SampleCommand() => new(
        Name: "Sídlo VŠE",
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
        BuildingTypeCode: "office",
        Latitude: 50.087,
        Longitude: 14.421,
        YearBuilt: 1990,
        YearRenovated: null);

    [Fact]
    public async Task Handle_PersistsBuildingOwnedByCurrentUser()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(SampleCommand());

        var building = Assert.Single(_repository.Buildings);
        Assert.Equal(building.Id, result.Id);
        Assert.StartsWith("bld-", result.UriSlug);
        Assert.Equal(building.UriSlug, result.UriSlug);
        Assert.Equal(ProjectionId, building.OwnerId);
        Assert.Equal(ProjectionId, building.CreatedBy);
        Assert.Equal(Now, building.CreatedAt);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_SeedsAllAttributeHistories()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(SampleCommand());

        var building = _repository.Buildings.Single();
        Assert.Single(building.NameHistory);
        Assert.Single(building.AddressHistory);
        Assert.Single(building.TypeHistory);
        Assert.Single(building.LocationHistory);
        Assert.Single(building.YearsHistory);
    }

    [Fact]
    public async Task Handle_WithNullCoordinates_StillRegisters()
    {
        var handler = CreateHandler();
        var command = SampleCommand() with { Latitude = null, Longitude = null };

        await handler.HandleAsync(command);

        var building = _repository.Buildings.Single();
        var location = Assert.Single(building.LocationHistory);
        Assert.Null(location.Coordinates);
    }

    [Fact]
    public async Task Handle_GeneratesDistinctSlugForEachBuilding()
    {
        var handler = CreateHandler();

        var first = await handler.HandleAsync(SampleCommand());
        var second = await handler.HandleAsync(SampleCommand());

        Assert.Equal(2, _repository.Buildings.Count);
        Assert.StartsWith("bld-", first.UriSlug);
        Assert.StartsWith("bld-", second.UriSlug);
        Assert.NotEqual(first.UriSlug, second.UriSlug);
    }

    [Fact]
    public async Task Handle_WithInvalidPsc_Throws()
    {
        var handler = CreateHandler();
        var command = SampleCommand() with { Psc = "abc" };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task Handle_WithInvalidHouseNumberType_Throws()
    {
        var handler = CreateHandler();
        var command = SampleCommand() with { HouseNumberType = "bogus" };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task Handle_WithUnknownBuildingType_Throws()
    {
        var handler = CreateHandler();
        var command = SampleCommand() with { BuildingTypeCode = "castle" }; // not in building-type

        await Assert.ThrowsAsync<UnknownCodelistCodeException>(() => handler.HandleAsync(command));
    }

    [Fact]
    public async Task Handle_WithOnlyOneOfLatLon_Throws()
    {
        var handler = CreateHandler();
        var command = SampleCommand() with { Latitude = 50.0, Longitude = null };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
    }
}
