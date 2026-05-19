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

    public RoomFloorHistory(Guid roomId, NpgsqlRange<DateTime> validity, byte floor, Guid recordedBy)
    {
        RoomId = roomId;
        Validity = validity;
        Floor = floor;
        RecordedAt = DateTime.UtcNow;
        RecordedBy = recordedBy;
    }

    public void Close(DateTime validFrom) => Validity = new NpgsqlRange<DateTime>(Validity.LowerBound, validFrom);
}
