using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Sensors;

/// <summary>Per-attribute history row for a sensor's hardware identity.</summary>
public sealed class SensorIdentityHistory : HistoryRow
{
    public Guid SensorId { get; init; }
    public string Manufacturer { get; init; } = null!;
    public string Model { get; init; } = null!;
    public string SerialNumber { get; init; } = null!;

    private SensorIdentityHistory() { }

    public SensorIdentityHistory(
        Guid sensorId,
        NpgsqlRange<DateTime> validity,
        string manufacturer,
        string model,
        string serialNumber,
        Guid recordedBy,
        DateTime recordedAt)
        : base(validity, recordedBy, recordedAt)
    {
        SensorId = sensorId;
        Manufacturer = manufacturer;
        Model = model;
        SerialNumber = serialNumber;
    }
}
