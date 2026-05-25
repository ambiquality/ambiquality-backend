using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomFloorHistory
{
    public Guid RoomId { get; init; }
    public NpgsqlRange<DateTime> Validity { get; set; }
    public byte Floor { get; init; }
    public DateTime RecordedAt { get; init; }
    public Guid RecordedBy { get; init; }

    private RoomFloorHistory() { }

    public RoomFloorHistory(Guid roomId, NpgsqlRange<DateTime> validity, byte floor, Guid recordedBy, DateTime recordedAt)
    {
        RoomId = roomId;
        Validity = validity;
        Floor = floor;
        RecordedAt = new DateTime(recordedAt.Ticks / 10 * 10, recordedAt.Kind);
        RecordedBy = recordedBy;
    }

    public void Close(DateTime validFrom) => Validity = Common.Validity.Closed(Validity.LowerBound, validFrom);
}
