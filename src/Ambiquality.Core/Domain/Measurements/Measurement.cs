namespace Ambiquality.Core.Domain.Measurements;

/// <summary>
/// A single observation reported by a sensor. References the canonical sensor
/// registry by <see cref="SensorId"/> (a GUID owned by Evidence.Api) with no
/// cross-database foreign key. Published measurements are immutable: values are
/// never updated or deleted, only soft-invalidated via <see cref="Invalidate"/>.
/// </summary>
public sealed class Measurement
{
    private Measurement()
    {
        ParameterCode = null!;
    }

    public Guid Id { get; private set; }
    public Guid SensorId { get; private set; }
    public string ParameterCode { get; private set; }
    public double Value { get; private set; }

    /// <summary>Nullable until F08 (measured-parameter units) lands in Evidence.</summary>
    public string? Unit { get; private set; }

    /// <summary>Instant the sensor recorded the observation (sensor clock).</summary>
    public DateTime ObservedAt { get; private set; }

    /// <summary>Instant the ingestion service accepted it (server clock); hypertable time column.</summary>
    public DateTime ReceivedAt { get; private set; }

    public bool IsInvalid { get; private set; }
    public string? InvalidatedReason { get; private set; }

    public static Measurement Record(
        Guid sensorId,
        string parameterCode,
        double value,
        string? unit,
        DateTime observedAt,
        DateTime receivedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterCode);

        return new Measurement
        {
            Id = Guid.NewGuid(),
            SensorId = sensorId,
            ParameterCode = parameterCode,
            Value = value,
            Unit = unit,
            ObservedAt = observedAt,
            ReceivedAt = receivedAt,
            IsInvalid = false,
            InvalidatedReason = null
        };
    }

    /// <summary>
    /// Soft-invalidation: flips the flag and records a reason without mutating
    /// the original value (Immutability NFR).
    /// </summary>
    public void Invalidate(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        IsInvalid = true;
        InvalidatedReason = reason;
    }
}
