using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomGeometryHistory
{
    public Guid RoomId { get; init; }
    public NpgsqlRange<DateTime> Validity { get; set; }
    public double? AreaM2 { get; init; }
    public double? CeilingHeightM { get; init; }
    public DateTime RecordedAt { get; init; }
    public Guid RecordedBy { get; init; }

    private RoomGeometryHistory() { }

    public RoomGeometryHistory(Guid roomId, NpgsqlRange<DateTime> validity, double? areaM2, double? ceilingHeightM, Guid recordedBy)
    {
        RoomId = roomId;
        Validity = validity;
        AreaM2 = areaM2;
        CeilingHeightM = ceilingHeightM;
        RecordedAt = DateTime.UtcNow;
        RecordedBy = recordedBy;
    }

    public void Close(DateTime validFrom) => Validity = new NpgsqlRange<DateTime>(Validity.LowerBound, validFrom);
}
