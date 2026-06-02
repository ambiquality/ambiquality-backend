using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomPollutionSourceHistory : HistoryRow
{
    public Guid RoomId { get; init; }
    public string SourceCode { get; init; } = null!;

    private RoomPollutionSourceHistory() { }

    public RoomPollutionSourceHistory(Guid roomId, string sourceCode, NpgsqlRange<DateTime> validity, Guid recordedBy, DateTime recordedAt)
        : base(validity, recordedBy, recordedAt)
    {
        RoomId = roomId;
        SourceCode = sourceCode;
    }
}
