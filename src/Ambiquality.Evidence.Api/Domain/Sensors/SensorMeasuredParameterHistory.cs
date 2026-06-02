using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Sensors;

/// <summary>
/// History row for one measured-parameter capability of a sensor. A sensor
/// measures several parameters at once, so unlike single-value attributes the
/// no-overlap rule is scoped per <see cref="ParameterCode"/>.
/// </summary>
public sealed class SensorMeasuredParameterHistory : HistoryRow
{
    public Guid SensorId { get; init; }
    public string ParameterCode { get; init; } = null!;

    private SensorMeasuredParameterHistory() { }

    public SensorMeasuredParameterHistory(
        Guid sensorId,
        string parameterCode,
        NpgsqlRange<DateTime> validity,
        Guid recordedBy,
        DateTime recordedAt)
        : base(validity, recordedBy, recordedAt)
    {
        SensorId = sensorId;
        ParameterCode = parameterCode;
    }
}
