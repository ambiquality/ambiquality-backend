namespace Ambiquality.Evidence.Api.Api;

public sealed record RegisterSensorRequest(
    string UriSlug,
    string Manufacturer,
    string Model,
    string SerialNumber,
    string StatusCode,
    IReadOnlyCollection<string> MeasuredParameters);

/// <summary>
/// Returned once from POST (register). Carries the plaintext <see cref="ApiKey"/>
/// — the only time it is ever exposed; it is not part of <see cref="SensorSnapshotResponse"/>
/// and never returned from reads.
/// </summary>
public sealed record SensorRegisteredResponse(
    Guid Id,
    string UriSlug,
    Guid BuildingId,
    Guid RoomId,
    string Manufacturer,
    string Model,
    string SerialNumber,
    string StatusCode,
    IReadOnlyCollection<string> MeasuredParameters,
    DateTime AsOf,
    string ApiKey);

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
