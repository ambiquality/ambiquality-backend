namespace Ambiquality.Public.Api.Api;

/// <summary>
/// One building marker in the map snapshot. Coordinates are already masked per the
/// building's anonymization level (see <see cref="CoordinateMasking"/>).
/// <see cref="LatestValue"/> is the mean of each contributing active sensor's most-recent
/// observation for the requested quantity; it is <c>null</c> when the freshest contributing
/// observation is older than the freshness window (then <see cref="Stale"/> is <c>true</c>).
/// </summary>
public sealed record MapSnapshotItem(
    Guid BuildingId,
    string Slug,
    string? Name,
    double? Lat,
    double? Lon,
    double? LatestValue,
    DateTime? ObservedAt,
    bool Stale,
    int SensorCount);

/// <summary>
/// The map snapshot: one item per building with ≥1 active sensor measuring
/// <see cref="ParameterCode"/>. <see cref="AsOf"/> is when the snapshot was computed.
/// </summary>
public sealed record MapSnapshotResponse(
    string ParameterCode,
    string? Unit,
    DateTime AsOf,
    IReadOnlyList<MapSnapshotItem> Items,
    string License);
