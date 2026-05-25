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

    public RoomGeometryHistory(Guid roomId, NpgsqlRange<DateTime> validity, double? areaM2, double? ceilingHeightM, Guid recordedBy, DateTime recordedAt)
    {
        RoomId = roomId;
        Validity = validity;
        AreaM2 = areaM2;
        CeilingHeightM = ceilingHeightM;
        RecordedAt = new DateTime(recordedAt.Ticks / 10 * 10, recordedAt.Kind);
        RecordedBy = recordedBy;
    }

    public void Close(DateTime validFrom) => Validity = Common.Validity.Closed(Validity.LowerBound, validFrom);
}
