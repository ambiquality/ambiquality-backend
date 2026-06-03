using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomGeometryHistory : HistoryRow
{
    public Guid RoomId { get; init; }
    public double? AreaM2 { get; init; }
    public double? CeilingHeightM { get; init; }

    private RoomGeometryHistory() { }

    public RoomGeometryHistory(Guid roomId, NpgsqlRange<DateTime> validity, double? areaM2, double? ceilingHeightM, Guid recordedBy, DateTime recordedAt)
        : base(validity, recordedBy, recordedAt)
    {
        RoomId = roomId;
        AreaM2 = areaM2;
        CeilingHeightM = ceilingHeightM;
    }
}
