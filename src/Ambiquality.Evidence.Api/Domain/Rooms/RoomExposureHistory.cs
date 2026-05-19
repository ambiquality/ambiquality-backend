using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomExposureHistory
{
    public Guid RoomId { get; init; }
    public NpgsqlRange<DateTime> Validity { get; set; }
    public string? ExposureCode { get; init; }
    public DateTime RecordedAt { get; init; }
    public Guid RecordedBy { get; init; }

    private RoomExposureHistory() { }

    public RoomExposureHistory(Guid roomId, NpgsqlRange<DateTime> validity, string? exposureCode, Guid recordedBy)
    {
        RoomId = roomId;
        Validity = validity;
        ExposureCode = exposureCode;
        RecordedAt = DateTime.UtcNow;
        RecordedBy = recordedBy;
    }

    public void Close(DateTime validFrom) => Validity = new NpgsqlRange<DateTime>(Validity.LowerBound, validFrom);
}
