using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomFunctionHistory : HistoryRow
{
    public Guid RoomId { get; init; }
    public string? FunctionCode { get; init; }

    private RoomFunctionHistory() { }

    public RoomFunctionHistory(Guid roomId, NpgsqlRange<DateTime> validity, string? functionCode, Guid recordedBy, DateTime recordedAt)
        : base(validity, recordedBy, recordedAt)
    {
        RoomId = roomId;
        FunctionCode = functionCode;
    }
}
