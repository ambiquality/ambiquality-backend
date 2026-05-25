using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomPollutionSourceHistory
{
    public Guid RoomId { get; init; }
    public string SourceCode { get; init; } = null!;
    public NpgsqlRange<DateTime> Validity { get; set; }
    public DateTime RecordedAt { get; init; }
    public Guid RecordedBy { get; init; }

    private RoomPollutionSourceHistory() { }

    public RoomPollutionSourceHistory(Guid roomId, string sourceCode, NpgsqlRange<DateTime> validity, Guid recordedBy, DateTime recordedAt)
    {
        RoomId = roomId;
        SourceCode = sourceCode;
        Validity = validity;
        RecordedAt = new DateTime(recordedAt.Ticks / 10 * 10, recordedAt.Kind);
        RecordedBy = recordedBy;
    }

    public void Close(DateTime validFrom) => Validity = Common.Validity.Closed(Validity.LowerBound, validFrom);
}
