using Ambiquality.Core.Domain.Vocabulary;

namespace Ambiquality.Evidence.Api.Api;

public sealed record RegisterSensorRequest(
    string Manufacturer,
    string Model,
    string SerialNumber,
    string StatusCode,
    IReadOnlyCollection<string> MeasuredParameters,
    SensorInstallationRequest? Installation = null);

/// <summary>
/// The optional supplementary installation details for a sensor (F08): its
/// position within the room, the distances to the nearest window / door /
/// pollution source (metres), the declared reporting interval (seconds) and the
/// installation / last-calibration dates. Every field is optional; supplying the
/// block with all fields null records nothing. Carried both at registration
/// (nested in <see cref="RegisterSensorRequest"/>) and on the change endpoint
/// (alongside <c>ValidFrom</c>).
/// </summary>
public sealed record SensorInstallationRequest(
    string? PositionNote,
    double? DistanceWindowM,
    double? DistanceDoorM,
    double? DistanceSourceM,
    int? MeasurementFrequencySeconds,
    DateOnly? InstalledOn,
    DateOnly? LastCalibratedOn);

/// <summary>
/// The installation details projected on a sensor read at the requested instant,
/// or <c>null</c> when the sensor had no installation row as of that time.
/// </summary>
public sealed record SensorInstallationResponse(
    string? PositionNote,
    double? DistanceWindowM,
    double? DistanceDoorM,
    double? DistanceSourceM,
    int? MeasurementFrequencySeconds,
    DateOnly? InstalledOn,
    DateOnly? LastCalibratedOn);

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
    SensorInstallationResponse? Installation,
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
    SensorInstallationResponse? Installation,
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

/// <summary>
/// Records new installation details (F08) effective from <c>ValidFrom</c>. The
/// body carries the complete new value of the installation attribute (every
/// field optional) plus the effective instant — the open history row is closed
/// half-open and a new one opens.
/// </summary>
public sealed record ChangeSensorInstallationRequest(
    string? PositionNote,
    double? DistanceWindowM,
    double? DistanceDoorM,
    double? DistanceSourceM,
    int? MeasurementFrequencySeconds,
    DateOnly? InstalledOn,
    DateOnly? LastCalibratedOn,
    DateTime ValidFrom);

public sealed record AddMeasuredParameterRequest(
    string ParameterCode,
    DateTime ValidFrom);

/// <summary>
/// Closes a measured-parameter capability's validity period as of <c>ValidTo</c>.
/// Carried in the body of <c>PUT …/measured-parameters/{code}</c>: the capability
/// is never physically deleted — its open history row is closed (soft history),
/// so the verb is PUT, not DELETE (RFC 9110 §9.3.4 vs §9.3.5).
/// </summary>
public sealed record RemoveMeasuredParameterRequest(
    DateTime ValidTo);
