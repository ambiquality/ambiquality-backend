using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomFloorHistory : HistoryRow
{
    public Guid RoomId { get; init; }
    public byte Floor { get; init; }

    private RoomFloorHistory() { }

    public RoomFloorHistory(Guid roomId, NpgsqlRange<DateTime> validity, byte floor, Guid recordedBy, DateTime recordedAt)
        : base(validity, recordedBy, recordedAt)
    {
        RoomId = roomId;
        Floor = floor;
    }
}
