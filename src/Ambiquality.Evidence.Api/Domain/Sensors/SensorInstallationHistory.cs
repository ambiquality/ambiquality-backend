using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Sensors;

/// <summary>
/// Per-attribute history row for a sensor's supplementary installation details
/// (F08): its position within the room, the distances to the nearest window,
/// door and pollution source, the declared reporting interval and the
/// installation / last-calibration dates. Every value field is optional — the
/// row exists only to record whatever the registrar chose to supply — and the
/// row as a whole is optional, so a sensor may have no installation history at
/// all.
/// </summary>
public sealed class SensorInstallationHistory : HistoryRow
{
    public Guid SensorId { get; init; }
    public string? PositionNote { get; init; }
    public double? DistanceWindowM { get; init; }
    public double? DistanceDoorM { get; init; }
    public double? DistanceSourceM { get; init; }
    public int? MeasurementFrequencySeconds { get; init; }
    public DateOnly? InstalledOn { get; init; }
    public DateOnly? LastCalibratedOn { get; init; }

    private SensorInstallationHistory() { }

    public SensorInstallationHistory(
        Guid sensorId,
        NpgsqlRange<DateTime> validity,
        string? positionNote,
        double? distanceWindowM,
        double? distanceDoorM,
        double? distanceSourceM,
        int? measurementFrequencySeconds,
        DateOnly? installedOn,
        DateOnly? lastCalibratedOn,
        Guid recordedBy,
        DateTime recordedAt)
        : base(validity, recordedBy, recordedAt)
    {
        SensorId = sensorId;
        PositionNote = positionNote;
        DistanceWindowM = distanceWindowM;
        DistanceDoorM = distanceDoorM;
        DistanceSourceM = distanceSourceM;
        MeasurementFrequencySeconds = measurementFrequencySeconds;
        InstalledOn = installedOn;
        LastCalibratedOn = lastCalibratedOn;
    }
}
