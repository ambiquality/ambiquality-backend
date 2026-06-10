using Ambiquality.Evidence.Api.Domain;

namespace Ambiquality.Evidence.Api.Domain.Sensors;

/// <summary>
/// The seven optional installation fields (F08), bundled into one value object
/// so the aggregate handles them as a single attribute. Every field is nullable;
/// the value object enforces the cross-field rules — distances and the reporting
/// frequency must be positive when supplied, and the last-calibration date must
/// not precede the installation date — and exposes <see cref="HasAnyValue"/> so a
/// caller can tell whether the registrar actually supplied anything worth
/// recording.
/// </summary>
public sealed record SensorInstallationDetails
{
    public string? PositionNote { get; }
    public double? DistanceWindowM { get; }
    public double? DistanceDoorM { get; }
    public double? DistanceSourceM { get; }
    public int? MeasurementFrequencySeconds { get; }
    public DateOnly? InstalledOn { get; }
    public DateOnly? LastCalibratedOn { get; }

    private SensorInstallationDetails(
        string? positionNote,
        double? distanceWindowM,
        double? distanceDoorM,
        double? distanceSourceM,
        int? measurementFrequencySeconds,
        DateOnly? installedOn,
        DateOnly? lastCalibratedOn)
    {
        PositionNote = positionNote;
        DistanceWindowM = distanceWindowM;
        DistanceDoorM = distanceDoorM;
        DistanceSourceM = distanceSourceM;
        MeasurementFrequencySeconds = measurementFrequencySeconds;
        InstalledOn = installedOn;
        LastCalibratedOn = lastCalibratedOn;
    }

    /// <summary>
    /// True when at least one field carries a value. An installation row is only
    /// opened when this holds — an all-null payload records nothing.
    /// </summary>
    public bool HasAnyValue =>
        PositionNote is not null
        || DistanceWindowM is not null
        || DistanceDoorM is not null
        || DistanceSourceM is not null
        || MeasurementFrequencySeconds is not null
        || InstalledOn is not null
        || LastCalibratedOn is not null;

    /// <summary>
    /// Validates and constructs the value object. A blank or whitespace-only
    /// position note is normalised to <c>null</c>; non-positive distances or
    /// frequency, and a calibration date before the installation date, are
    /// rejected with a <see cref="DomainException"/> (the API maps it to a 400).
    /// </summary>
    public static SensorInstallationDetails Create(
        string? positionNote,
        double? distanceWindowM,
        double? distanceDoorM,
        double? distanceSourceM,
        int? measurementFrequencySeconds,
        DateOnly? installedOn,
        DateOnly? lastCalibratedOn)
    {
        var note = string.IsNullOrWhiteSpace(positionNote) ? null : positionNote.Trim();

        EnsurePositive(distanceWindowM, "Distance to the nearest window");
        EnsurePositive(distanceDoorM, "Distance to the nearest door");
        EnsurePositive(distanceSourceM, "Distance to the nearest pollution source");
        EnsurePositive(measurementFrequencySeconds, "Measurement frequency");

        if (installedOn is { } from && lastCalibratedOn is { } to && to < from)
            throw new DomainException(
                "Last calibration date must not be before the installation date.");

        return new SensorInstallationDetails(
            note,
            distanceWindowM,
            distanceDoorM,
            distanceSourceM,
            measurementFrequencySeconds,
            installedOn,
            lastCalibratedOn);
    }

    private static void EnsurePositive(double? value, string label)
    {
        if (value is { } v && v <= 0)
            throw new DomainException($"{label} must be a positive number of metres when provided.");
    }

    private static void EnsurePositive(int? value, string label)
    {
        if (value is { } v && v <= 0)
            throw new DomainException($"{label} must be a positive number of seconds when provided.");
    }
}
