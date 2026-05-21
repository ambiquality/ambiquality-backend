namespace Ambiquality.Evidence.Api.Application.Rooms;

public sealed record RegisterRoomCommand(
    Guid BuildingId,
    string UriSlug,
    string Name,
    byte Floor,
    string? FunctionCode,
    string? ExposureCode,
    double? AreaM2,
    double? CeilingHeightM,
    string? VentilationType,
    IReadOnlyCollection<string> PollutionSources);

public sealed record RegisterRoomResponse(Guid RoomId, string UriSlug);

public sealed record ChangeRoomNameCommand(Guid RoomId, string NewName, DateTime ValidFrom);
public sealed record ChangeRoomFloorCommand(Guid RoomId, byte NewFloor, DateTime ValidFrom);
public sealed record ChangeRoomFunctionCommand(Guid RoomId, string? NewFunctionCode, DateTime ValidFrom);
public sealed record ChangeRoomExposureCommand(Guid RoomId, string? NewExposureCode, DateTime ValidFrom);
public sealed record ChangeRoomGeometryCommand(Guid RoomId, double? AreaM2, double? CeilingHeightM, DateTime ValidFrom);
public sealed record ChangeRoomVentilationCommand(Guid RoomId, string? NewVentilationType, DateTime ValidFrom);

public sealed record AddRoomPollutionSourceCommand(Guid RoomId, string SourceCode, DateTime ValidFrom);
public sealed record RemoveRoomPollutionSourceCommand(Guid RoomId, string SourceCode, DateTime ValidTo);
