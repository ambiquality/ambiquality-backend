using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomNameHistory
{
    public Guid RoomId { get; init; }
    public NpgsqlRange<DateTime> Validity { get; set; }
    public string Name { get; init; } = null!;
    public DateTime RecordedAt { get; init; }
    public Guid RecordedBy { get; init; }

    private RoomNameHistory() { }

    public RoomNameHistory(Guid roomId, NpgsqlRange<DateTime> validity, string name, Guid recordedBy)
    {
        RoomId = roomId;
        Validity = validity;
        Name = name;
        RecordedAt = DateTime.UtcNow;
        RecordedBy = recordedBy;
    }

    public void Close(DateTime validFrom) => Validity = new NpgsqlRange<DateTime>(Validity.LowerBound, validFrom);
}
