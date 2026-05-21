namespace Ambiquality.Evidence.Api.Api;

public sealed record RegisterRoomRequest(
    string UriSlug,
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

public sealed record ChangeRoomGeometryRequest(
    double? AreaM2,
    double? CeilingHeightM,
    DateTime ValidFrom);

public sealed record AddPollutionSourceRequest(
    string SourceCode,
    DateTime ValidFrom);

public sealed record RemovePollutionSourceRequest(
    DateTime ValidTo);
