using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Sensors;

/// <summary>Per-attribute history row for a sensor's hardware identity.</summary>
public sealed class SensorIdentityHistory
{
    public Guid SensorId { get; init; }
    public NpgsqlRange<DateTime> Validity { get; set; }
    public string Manufacturer { get; init; } = null!;
    public string Model { get; init; } = null!;
    public string SerialNumber { get; init; } = null!;
    public DateTime RecordedAt { get; init; }
    public Guid RecordedBy { get; init; }

    private SensorIdentityHistory() { }

    public SensorIdentityHistory(
        Guid sensorId,
        NpgsqlRange<DateTime> validity,
        string manufacturer,
        string model,
        string serialNumber,
        Guid recordedBy,
        DateTime recordedAt)
    {
        SensorId = sensorId;
        Validity = validity;
        Manufacturer = manufacturer;
        Model = model;
        SerialNumber = serialNumber;
        RecordedAt = new DateTime(recordedAt.Ticks / 10 * 10, recordedAt.Kind);
        RecordedBy = recordedBy;
    }

    // Half-open [lower, validFrom): the upper bound is EXCLUSIVE so the closed
    // row and the next open row (which starts at validFrom) do not both contain
    // the boundary instant. The 2-arg NpgsqlRange ctor would make it inclusive.
    public void Close(DateTime validFrom) =>
        Validity = new NpgsqlRange<DateTime>(Validity.LowerBound, true, false, validFrom, false, false);
}
