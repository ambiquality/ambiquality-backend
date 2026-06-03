extern alias PublicApi;
using PublicApi::Ambiquality.Public.Api.Application.Observations;
using PublicApi::Ambiquality.Public.Api.Infrastructure.Catalog;

namespace Ambiquality.Public.Api.Tests;

/// <summary>
/// DB-free coverage of observation-time placement resolution: a sensor that relocates
/// must report the room it occupied *at the observation's time*, not its latest room.
/// </summary>
public sealed class FeatureOfInterestResolverTests
{
    private static readonly Guid Sensor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid RoomA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid RoomB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Building = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTime Move = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    // RoomA over [Jan, Mar), then RoomB over [Mar, open).
    private static FeatureOfInterestResolver Relocating() => new(
    [
        new SensorPlacement(Sensor, RoomA, Building, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Move),
        new SensorPlacement(Sensor, RoomB, Building, Move, ValidTo: null)
    ]);

    [Fact]
    public void Resolves_RoomOccupiedAtObservationTime_NotLatest()
    {
        var foi = Relocating();

        Assert.Equal(RoomA, foi.ResolveRoomId(Sensor, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(RoomB, foi.ResolveRoomId(Sensor, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Boundary_IsHalfOpen_LowerInclusiveUpperExclusive()
    {
        var foi = Relocating();

        // The move instant belongs to the new period, not the old one.
        Assert.Equal(RoomB, foi.ResolveRoomId(Sensor, Move));
    }

    [Fact]
    public void BeforeFirstPlacement_ResolvesNull()
    {
        var foi = Relocating();
        Assert.Null(foi.ResolveRoomId(Sensor, new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void UnknownSensor_ResolvesNull()
    {
        var foi = Relocating();
        Assert.Null(foi.ResolveRoomId(Guid.NewGuid(), Move));
    }
}
