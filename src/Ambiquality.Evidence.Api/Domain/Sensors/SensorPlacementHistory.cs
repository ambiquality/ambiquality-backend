using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Sensors;

/// <summary>
/// Per-attribute history row for where a sensor is installed. Tracks both the
/// room and its building so a snapshot can project placement without a join,
/// and so relocations between rooms are auditable over time.
/// </summary>
public sealed class SensorPlacementHistory
{
    public Guid SensorId { get; init; }
    public NpgsqlRange<DateTime> Validity { get; set; }
    public Guid BuildingId { get; init; }
    public Guid RoomId { get; init; }
    public DateTime RecordedAt { get; init; }
    public Guid RecordedBy { get; init; }

    private SensorPlacementHistory() { }

    public SensorPlacementHistory(
        Guid sensorId,
        NpgsqlRange<DateTime> validity,
        Guid buildingId,
        Guid roomId,
        Guid recordedBy,
        DateTime recordedAt)
    {
        SensorId = sensorId;
        Validity = validity;
        BuildingId = buildingId;
        RoomId = roomId;
        RecordedAt = new DateTime(recordedAt.Ticks / 10 * 10, recordedAt.Kind);
        RecordedBy = recordedBy;
    }

    // Half-open [lower, validFrom): exclusive upper so the closed row and the
    // next open row do not both contain the boundary instant.
    public void Close(DateTime validFrom) =>
        Validity = new NpgsqlRange<DateTime>(Validity.LowerBound, true, false, validFrom, false, false);
}
