using Ambiquality.Core.Messaging;

namespace Ambiquality.Core.Tests.Messaging;

public class MeasurementMessageSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new MeasurementMessage(
            Id: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            ParameterCode: "co2",
            Value: 812.5,
            Unit: "ppm",
            ObservedAt: new DateTime(2026, 5, 28, 9, 30, 15, DateTimeKind.Utc),
            ReceivedAt: new DateTime(2026, 5, 28, 9, 30, 16, 123, DateTimeKind.Utc));

        var restored = MeasurementMessageSerializer.Deserialize(
            MeasurementMessageSerializer.Serialize(original));

        Assert.Equal(original, restored);
    }

    [Fact]
    public void RoundTrip_PreservesReceivedAtExactlyAsUtc()
    {
        // The hard ingestion requirement: the acceptance timestamp must survive the
        // queue byte-for-byte (same ticks, same UTC kind), so worker lag never moves it.
        var receivedAt = new DateTime(2026, 5, 28, 9, 30, 16, 123, DateTimeKind.Utc).AddTicks(4567);
        var original = new MeasurementMessage(
            Guid.NewGuid(), Guid.NewGuid(), "co2", 800, null, DateTime.UtcNow, receivedAt);

        var restored = MeasurementMessageSerializer.Deserialize(
            MeasurementMessageSerializer.Serialize(original));

        Assert.Equal(receivedAt.Ticks, restored.ReceivedAt.Ticks);
        Assert.Equal(DateTimeKind.Utc, restored.ReceivedAt.Kind);
    }

    [Fact]
    public void RoundTrip_PreservesNullUnit()
    {
        var original = new MeasurementMessage(
            Guid.NewGuid(), Guid.NewGuid(), "co2", 800, null, DateTime.UtcNow, DateTime.UtcNow);

        var restored = MeasurementMessageSerializer.Deserialize(
            MeasurementMessageSerializer.Serialize(original));

        Assert.Null(restored.Unit);
    }
}
