using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomVentilationHistory
{
    public Guid RoomId { get; init; }
    public NpgsqlRange<DateTime> Validity { get; set; }
    public string? VentilationType { get; init; }
    public DateTime RecordedAt { get; init; }
    public Guid RecordedBy { get; init; }

    private RoomVentilationHistory() { }

    public RoomVentilationHistory(Guid roomId, NpgsqlRange<DateTime> validity, string? ventilationType, Guid recordedBy)
    {
        RoomId = roomId;
        Validity = validity;
        VentilationType = ventilationType;
        RecordedAt = DateTime.UtcNow;
        RecordedBy = recordedBy;
    }

    public void Close(DateTime validFrom) => Validity = new NpgsqlRange<DateTime>(Validity.LowerBound, validFrom);
}
