namespace Ambiquality.Evidence.Api.Application.Sensors;

public sealed record RegisterSensorCommand(
    Guid BuildingId,
    Guid RoomId,
    string UriSlug,
    string Manufacturer,
    string Model,
    string SerialNumber,
    string StatusCode,
    IReadOnlyCollection<string> MeasuredParameters);

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
