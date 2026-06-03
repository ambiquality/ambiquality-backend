using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Sensors;

/// <summary>Per-attribute history row for a sensor's operational status code.</summary>
public sealed class SensorStatusHistory : HistoryRow
{
    public Guid SensorId { get; init; }
    public string StatusCode { get; init; } = null!;

    private SensorStatusHistory() { }

    public SensorStatusHistory(
        Guid sensorId,
        NpgsqlRange<DateTime> validity,
        string statusCode,
        Guid recordedBy,
        DateTime recordedAt)
        : base(validity, recordedBy, recordedAt)
    {
        SensorId = sensorId;
        StatusCode = statusCode;
    }
}
