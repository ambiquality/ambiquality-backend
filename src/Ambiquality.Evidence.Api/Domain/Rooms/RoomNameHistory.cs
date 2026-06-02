using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomNameHistory : HistoryRow
{
    public Guid RoomId { get; init; }
    public string Name { get; init; } = null!;

    private RoomNameHistory() { }

    public RoomNameHistory(Guid roomId, NpgsqlRange<DateTime> validity, string name, Guid recordedBy, DateTime recordedAt)
        : base(validity, recordedBy, recordedAt)
    {
        RoomId = roomId;
        Name = name;
    }
}
