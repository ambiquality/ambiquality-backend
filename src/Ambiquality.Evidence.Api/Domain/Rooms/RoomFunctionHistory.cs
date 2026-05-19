using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomFunctionHistory
{
    public Guid RoomId { get; init; }
    public NpgsqlRange<DateTime> Validity { get; set; }
    public string? FunctionCode { get; init; }
    public DateTime RecordedAt { get; init; }
    public Guid RecordedBy { get; init; }

    private RoomFunctionHistory() { }

    public RoomFunctionHistory(Guid roomId, NpgsqlRange<DateTime> validity, string? functionCode, Guid recordedBy)
    {
        RoomId = roomId;
        Validity = validity;
        FunctionCode = functionCode;
        RecordedAt = DateTime.UtcNow;
        RecordedBy = recordedBy;
    }

    public void Close(DateTime validFrom) => Validity = new NpgsqlRange<DateTime>(Validity.LowerBound, validFrom);
}
