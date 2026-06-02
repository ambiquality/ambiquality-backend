using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Sensors;

/// <summary>
/// Per-attribute history row for where a sensor is installed. Tracks both the
/// room and its building so a snapshot can project placement without a join,
/// and so relocations between rooms are auditable over time.
/// </summary>
public sealed class SensorPlacementHistory : HistoryRow
{
    public Guid SensorId { get; init; }
    public Guid BuildingId { get; init; }
    public Guid RoomId { get; init; }

    private SensorPlacementHistory() { }

    public SensorPlacementHistory(
        Guid sensorId,
        NpgsqlRange<DateTime> validity,
        Guid buildingId,
        Guid roomId,
        Guid recordedBy,
        DateTime recordedAt)
        : base(validity, recordedBy, recordedAt)
    {
        SensorId = sensorId;
        BuildingId = buildingId;
        RoomId = roomId;
    }
}
