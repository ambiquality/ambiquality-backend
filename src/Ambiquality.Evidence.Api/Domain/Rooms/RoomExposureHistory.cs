using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public sealed class RoomExposureHistory : HistoryRow
{
    public Guid RoomId { get; init; }
    public string? ExposureCode { get; init; }

    private RoomExposureHistory() { }

    public RoomExposureHistory(Guid roomId, NpgsqlRange<DateTime> validity, string? exposureCode, Guid recordedBy, DateTime recordedAt)
        : base(validity, recordedBy, recordedAt)
    {
        RoomId = roomId;
        ExposureCode = exposureCode;
    }
}
