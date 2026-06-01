using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Evidence.Api.Api;

public sealed record RegisterSensorRequest(
    string Manufacturer,
    string Model,
    string SerialNumber,
    string StatusCode,
    IReadOnlyCollection<string> MeasuredParameters);

/// <summary>
/// A measured parameter with its QUDT quantity kind and unit URIs, enabling
/// 5-star linked open data per STA01/STA04 thesis requirements.
/// </summary>
/// <param name="Code">Internal parameter code (e.g. "co2").</param>
/// <param name="QuantityKindUri">QUDT quantity kind URI, or null if unknown.</param>
/// <param name="UnitUri">QUDT unit URI, or null if unknown.</param>
public sealed record MeasuredParameterResponse(
    string Code,
    string? QuantityKindUri,
    string? UnitUri)
{
    public static MeasuredParameterResponse FromCode(string code)
    {
        var qudt = QudtVocabulary.TryResolve(code);
        return new MeasuredParameterResponse(code, qudt?.QuantityKindUri, qudt?.UnitUri);
    }
}

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
    IReadOnlyCollection<MeasuredParameterResponse> MeasuredParameters,
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
    IReadOnlyCollection<MeasuredParameterResponse> MeasuredParameters,
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
