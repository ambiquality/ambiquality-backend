namespace Ambiquality.Evidence.Api.Api;

/// <summary>
/// Room registration payload. <c>ExposureCode</c>, when supplied, must be one of the
/// <see cref="Ambiquality.Core.Domain.Rooms.ExposureCode"/> codelist values
/// (<c>short</c>, <c>medium</c>, <c>long</c> — typical occupant-stay duration);
/// an unknown value is rejected with 400. The same rule applies to the
/// <c>/exposure</c> change endpoint.
/// </summary>
public sealed record RegisterRoomRequest(
    string Name,
    byte Floor,
    string? FunctionCode,
    string? ExposureCode,
    double? AreaM2,
    double? CeilingHeightM,
    string? VentilationType,
    IReadOnlyCollection<string> PollutionSources);

public sealed record RoomSnapshotResponse(
    Guid Id,
    string UriSlug,
    Guid BuildingId,
    string Name,
    byte Floor,
    string? FunctionCode,
    string? ExposureCode,
    double? AreaM2,
    double? CeilingHeightM,
    string? VentilationType,
    IReadOnlyCollection<string> PollutionSources,
    DateTime AsOf);

public sealed record ChangeRoomAttributeRequest(
    string NewValue,
    DateTime ValidFrom);

/// <summary>
/// Typed floor change: the framework binds and range-checks <c>Floor</c> as a byte,
/// so a non-numeric, negative or &gt;255 value is rejected as 400 before the handler
/// runs — the handler never parses a string. The domain accepts 0–100, so the
/// endpoint additionally rejects 101–255 as a 400.
/// </summary>
public sealed record ChangeRoomFloorRequest(
    byte Floor,
    DateTime ValidFrom);

public sealed record ChangeRoomGeometryRequest(
    double? AreaM2,
    double? CeilingHeightM,
    DateTime ValidFrom);

public sealed record AddPollutionSourceRequest(
    string SourceCode,
    DateTime ValidFrom);

public sealed record RemovePollutionSourceRequest(
    DateTime ValidTo);
