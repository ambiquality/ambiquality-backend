using Ambiquality.Public.Api.Api;

namespace Ambiquality.Public.Api.Infrastructure.Catalog;

/// <summary>Raw current-state building row read from the evidence schema (pre-masking).</summary>
public sealed record BuildingRow(
    Guid Id,
    string? Name,
    string? Street,
    string? City,
    string? Postcode,
    string? Country,
    string? BuildingTypeCode,
    double? Latitude,
    double? Longitude,
    string? Anonymization,
    int? YearBuilt,
    int? YearRenovated);

/// <summary>Raw current-state room row read from the evidence schema.</summary>
public sealed record RoomRow(
    Guid Id,
    Guid BuildingId,
    string? Name,
    byte Floor,
    string? FunctionCode,
    string? ExposureCode,
    double? AreaM2,
    double? CeilingHeightM,
    string? VentilationType,
    IReadOnlyList<string> PollutionSources);

/// <summary>Raw current-state sensor row read from the evidence schema.</summary>
public sealed record SensorRow(
    Guid Id,
    Guid BuildingId,
    Guid RoomId,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? StatusCode,
    IReadOnlyList<string> MeasuredParameterCodes);

/// <summary>Geographic extent of the building stock, for the DCAT spatial coverage.</summary>
public sealed record SpatialExtent(double MinLat, double MinLon, double MaxLat, double MaxLon);

/// <summary>
/// Read-only access to the Evidence catalog (buildings, rooms, sensors) over a
/// dedicated <c>public_api</c> connection. Queries are schema-qualified
/// (<c>evidence.*</c>) and select the currently open temporal rows via
/// <c>upper_inf(validity)</c> — the same pattern as Ingestion.Api's SensorCatalog.
/// </summary>
public interface IEvidenceCatalog
{
    Task<(IReadOnlyList<BuildingRow> Rows, long Total)> GetBuildingsAsync(
        string? buildingType, BoundingBox? bbox, int page, int pageSize, CancellationToken ct);

    Task<BuildingRow?> GetBuildingAsync(Guid id, CancellationToken ct);

    Task<(IReadOnlyList<RoomRow> Rows, long Total)> GetRoomsAsync(
        Guid buildingId, string? functionCode, int? minExposureMinutes, int page, int pageSize, CancellationToken ct);

    Task<RoomRow?> GetRoomAsync(Guid id, CancellationToken ct);

    Task<(IReadOnlyList<SensorRow> Rows, long Total)> GetSensorsAsync(
        Guid roomId, string? parameterCode, string? status, int page, int pageSize, CancellationToken ct);

    Task<SensorRow?> GetSensorAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Resolves the set of sensor ids matching a building / room / bounding-box filter,
    /// used to scope observation queries. An empty set means "no matching sensors".
    /// </summary>
    Task<IReadOnlyCollection<Guid>> ResolveSensorIdsAsync(
        Guid? buildingId, Guid? roomId, BoundingBox? bbox, CancellationToken ct);

    /// <summary>Bounding box of all current building coordinates, or null when none are set.</summary>
    Task<SpatialExtent?> GetSpatialExtentAsync(CancellationToken ct);
}
