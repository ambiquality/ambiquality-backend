using Ambiquality.Evidence.Api.Domain.Sensors;

namespace Ambiquality.Evidence.Api.Application.Sensors;

public sealed record RegisterSensorCommand(
    Guid BuildingId,
    Guid RoomId,
    string Manufacturer,
    string Model,
    string SerialNumber,
    string StatusCode,
    IReadOnlyCollection<string> MeasuredParameters,
    SensorInstallationInput? Installation = null);

/// <summary>
/// The seven optional installation fields (F08) carried unparsed from the edge.
/// Mapped to the <see cref="Domain.Sensors.SensorInstallationDetails"/> value
/// object (which validates them) by the register / change handlers.
/// </summary>
public sealed record SensorInstallationInput(
    string? PositionNote,
    double? DistanceWindowM,
    double? DistanceDoorM,
    double? DistanceSourceM,
    int? MeasurementFrequencySeconds,
    DateOnly? InstalledOn,
    DateOnly? LastCalibratedOn)
{
    /// <summary>Maps to the validating domain value object.</summary>
    public SensorInstallationDetails ToDetails() =>
        SensorInstallationDetails.Create(
            PositionNote,
            DistanceWindowM,
            DistanceDoorM,
            DistanceSourceM,
            MeasurementFrequencySeconds,
            InstalledOn,
            LastCalibratedOn);
}

public sealed record RegisterSensorResponse(Guid SensorId, string UriSlug, string ApiKey);

public sealed record ChangeSensorIdentityCommand(
    Guid SensorId,
    string Manufacturer,
    string Model,
    string SerialNumber,
    DateTime ValidFrom);

public sealed record ChangeSensorPlacementCommand(
    Guid SensorId,
    Guid NewRoomId,
    DateTime ValidFrom);

public sealed record ChangeSensorStatusCommand(Guid SensorId, string NewStatusCode, DateTime ValidFrom);

public sealed record AddSensorMeasuredParameterCommand(Guid SensorId, string ParameterCode, DateTime ValidFrom);
public sealed record RemoveSensorMeasuredParameterCommand(Guid SensorId, string ParameterCode, DateTime ValidTo);

public sealed record ChangeSensorInstallationCommand(
    Guid SensorId,
    SensorInstallationInput Installation,
    DateTime ValidFrom);
