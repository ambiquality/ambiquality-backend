namespace Ambiquality.Evidence.Api.Api;

public sealed record RegisterSensorRequest(
    string UriSlug,
    string Manufacturer,
    string Model,
    string SerialNumber,
    string StatusCode,
    IReadOnlyCollection<string> MeasuredParameters);

public sealed record SensorSnapshotResponse(
    Guid Id,
    string UriSlug,
    Guid BuildingId,
    Guid RoomId,
    string Manufacturer,
    string Model,
    string SerialNumber,
    string StatusCode,
    IReadOnlyCollection<string> MeasuredParameters,
    DateTime AsOf);

public sealed record ChangeSensorIdentityRequest(
    string Manufacturer,
    string Model,
    string SerialNumber,
    DateTime ValidFrom);

public sealed record ChangeSensorPlacementRequest(
    Guid NewRoomId,
    DateTime ValidFrom);

public sealed record ChangeSensorStatusRequest(
    string NewStatusCode,
    DateTime ValidFrom);

public sealed record AddMeasuredParameterRequest(
    string ParameterCode,
    DateTime ValidFrom);
