using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomBuildingHistory
{
    public Guid RoomId { get; init; }
    public NpgsqlRange<DateTime> Validity { get; set; }
    public Guid BuildingId { get; init; }
    public DateTime RecordedAt { get; init; }
    public Guid RecordedBy { get; init; }

    private RoomBuildingHistory() { }

    public RoomBuildingHistory(Guid roomId, NpgsqlRange<DateTime> validity, Guid buildingId, Guid recordedBy, DateTime recordedAt)
    {
        RoomId = roomId;
        Validity = validity;
        BuildingId = buildingId;
        RecordedAt = new DateTime(recordedAt.Ticks / 10 * 10, recordedAt.Kind);
        RecordedBy = recordedBy;
    }

    public void Close(DateTime validFrom) => Validity = Common.Validity.Closed(Validity.LowerBound, validFrom);
}
