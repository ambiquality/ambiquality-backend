namespace Ambiquality.Export.Worker.Persistence;

/// <summary>
/// One placement period of a sensor — the room it occupied over a half-open
/// <c>[ValidFrom, ValidTo)</c> UTC window (<see cref="ValidTo"/> is null while open).
/// Read from the evidence catalog to resolve an observation's feature of interest.
/// </summary>
public readonly record struct SensorPlacement(
    Guid SensorId, Guid RoomId, DateTime ValidFrom, DateTime? ValidTo);
