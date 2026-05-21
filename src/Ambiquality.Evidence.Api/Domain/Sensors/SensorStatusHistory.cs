using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Sensors;

/// <summary>Per-attribute history row for a sensor's operational status code.</summary>
public sealed class SensorStatusHistory
{
    public Guid SensorId { get; init; }
    public NpgsqlRange<DateTime> Validity { get; set; }
    public string StatusCode { get; init; } = null!;
    public DateTime RecordedAt { get; init; }
    public Guid RecordedBy { get; init; }

    private SensorStatusHistory() { }

    public SensorStatusHistory(
        Guid sensorId,
        NpgsqlRange<DateTime> validity,
        string statusCode,
        Guid recordedBy,
        DateTime recordedAt)
    {
        SensorId = sensorId;
        Validity = validity;
        StatusCode = statusCode;
        RecordedAt = new DateTime(recordedAt.Ticks / 10 * 10, recordedAt.Kind);
        RecordedBy = recordedBy;
    }

    // Half-open [lower, validFrom): exclusive upper so the closed row and the
    // next open row do not both contain the boundary instant.
    public void Close(DateTime validFrom) =>
        Validity = new NpgsqlRange<DateTime>(Validity.LowerBound, true, false, validFrom, false, false);
}
