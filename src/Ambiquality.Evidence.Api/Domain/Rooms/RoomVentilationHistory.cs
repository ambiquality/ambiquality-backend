using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomVentilationHistory : HistoryRow
{
    public Guid RoomId { get; init; }
    public string? VentilationType { get; init; }

    private RoomVentilationHistory() { }

    public RoomVentilationHistory(Guid roomId, NpgsqlRange<DateTime> validity, string? ventilationType, Guid recordedBy, DateTime recordedAt)
        : base(validity, recordedBy, recordedAt)
    {
        RoomId = roomId;
        VentilationType = ventilationType;
    }
}
