using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Sensors;

/// <summary>
/// History row for one measured-parameter capability of a sensor. A sensor
/// measures several parameters at once, so unlike single-value attributes the
/// no-overlap rule is scoped per <see cref="ParameterCode"/>.
/// </summary>
public sealed class SensorMeasuredParameterHistory
{
    public Guid SensorId { get; init; }
    public string ParameterCode { get; init; } = null!;
    public NpgsqlRange<DateTime> Validity { get; set; }
    public DateTime RecordedAt { get; init; }
    public Guid RecordedBy { get; init; }

    private SensorMeasuredParameterHistory() { }

    public SensorMeasuredParameterHistory(
        Guid sensorId,
        string parameterCode,
        NpgsqlRange<DateTime> validity,
        Guid recordedBy,
        DateTime recordedAt)
    {
        SensorId = sensorId;
        ParameterCode = parameterCode;
        Validity = validity;
        RecordedAt = new DateTime(recordedAt.Ticks / 10 * 10, recordedAt.Kind);
        RecordedBy = recordedBy;
    }

    // Half-open [lower, validFrom): exclusive upper so a closed row and a later
    // re-added row for the same parameter do not both contain the boundary.
    public void Close(DateTime validFrom) =>
        Validity = new NpgsqlRange<DateTime>(Validity.LowerBound, true, false, validFrom, false, false);
}
