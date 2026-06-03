using Ambiquality.Export.Worker.Persistence;
using Ambiquality.Export.Worker.Serialization;

namespace Ambiquality.Export.Worker.Tests;

/// <summary>Observation-time placement resolution for the export's feature of interest.</summary>
public sealed class FeatureOfInterestResolverTests
{
    private static readonly Guid Sensor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid RoomA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid RoomB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly DateTime Move = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private static FeatureOfInterestResolver Relocating() => new(
    [
        new SensorPlacement(Sensor, RoomA, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Move),
        new SensorPlacement(Sensor, RoomB, Move, null)
    ]);

    [Fact]
    public void Resolves_RoomOccupiedAtObservationTime_NotLatest()
    {
        var foi = Relocating();
        Assert.Equal(RoomA, foi.ResolveRoomId(Sensor, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(RoomB, foi.ResolveRoomId(Sensor, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Boundary_IsHalfOpen_AndUnknownsResolveNull()
    {
        var foi = Relocating();
        Assert.Equal(RoomB, foi.ResolveRoomId(Sensor, Move)); // move instant belongs to the new period
        Assert.Null(foi.ResolveRoomId(Sensor, new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Null(foi.ResolveRoomId(Guid.NewGuid(), Move));
        Assert.Null(FeatureOfInterestResolver.Empty.ResolveRoomId(Sensor, Move));
    }
}
