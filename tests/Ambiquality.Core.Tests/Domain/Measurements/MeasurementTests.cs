using Ambiquality.Core.Domain.Measurements;

namespace Ambiquality.Core.Tests.Domain.Measurements;

public sealed class MeasurementTests
{
    private static Measurement Sample() => Measurement.Record(
        sensorId: Guid.NewGuid(),
        parameterCode: "co2",
        value: 412.5,
        unit: "ppm",
        observedAt: new DateTime(2026, 5, 26, 8, 0, 0, DateTimeKind.Utc),
        receivedAt: new DateTime(2026, 5, 26, 8, 0, 1, DateTimeKind.Utc));

    [Fact]
    public void Record_assigns_an_id_and_starts_valid()
    {
        var m = Sample();

        Assert.NotEqual(Guid.Empty, m.Id);
        Assert.False(m.IsInvalid);
        Assert.Null(m.InvalidatedReason);
    }

    [Fact]
    public void Record_preserves_all_supplied_values()
    {
        var sensorId = Guid.NewGuid();
        var observedAt = new DateTime(2026, 5, 26, 8, 0, 0, DateTimeKind.Utc);
        var receivedAt = observedAt.AddSeconds(1);

        var m = Measurement.Record(sensorId, "temperature", 21.3, "°C", observedAt, receivedAt);

        Assert.Equal(sensorId, m.SensorId);
        Assert.Equal("temperature", m.ParameterCode);
        Assert.Equal(21.3, m.Value);
        Assert.Equal("°C", m.Unit);
        Assert.Equal(observedAt, m.ObservedAt);
        Assert.Equal(receivedAt, m.ReceivedAt);
    }

    [Fact]
    public void Record_allows_a_null_unit_until_F08()
    {
        var m = Measurement.Record(Guid.NewGuid(), "co2", 400, null,
            DateTime.UtcNow, DateTime.UtcNow);

        Assert.Null(m.Unit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_rejects_blank_parameter_code(string code)
    {
        Assert.Throws<ArgumentException>(() => Measurement.Record(
            Guid.NewGuid(), code, 1, "ppm", DateTime.UtcNow, DateTime.UtcNow));
    }

    [Fact]
    public void Invalidate_flips_the_flag_without_touching_the_value()
    {
        var m = Sample();
        var original = m.Value;

        m.Invalidate("sensor recalibration");

        Assert.True(m.IsInvalid);
        Assert.Equal("sensor recalibration", m.InvalidatedReason);
        Assert.Equal(original, m.Value);
    }

    [Fact]
    public void Invalidate_requires_a_reason()
    {
        var m = Sample();
        Assert.Throws<ArgumentException>(() => m.Invalidate(" "));
    }
}
