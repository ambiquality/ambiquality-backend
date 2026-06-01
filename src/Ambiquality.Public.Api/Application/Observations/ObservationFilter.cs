using Ambiquality.Core.Domain.Measurements;
using Ambiquality.Public.Api.Api;

namespace Ambiquality.Public.Api.Application.Observations;

/// <summary>
/// Parsed, validated filters for an observation query. <see cref="Limit"/> is already
/// clamped to [1, <see cref="Constants.MaxPageSize"/>]; <see cref="Cursor"/> is the
/// decoded keyset position (or null for the first page).
/// </summary>
public sealed record ObservationFilter(
    DateTime? From,
    DateTime? To,
    Guid? SensorId,
    string? ParameterCode,
    Guid? BuildingId,
    Guid? RoomId,
    BoundingBox? Bbox,
    bool IncludeInvalid,
    int Limit,
    ObservationCursor? Cursor)
{
    /// <summary>True when a building/room/bbox filter requires resolving sensor ids first.</summary>
    public bool NeedsSensorResolution => BuildingId is not null || RoomId is not null || Bbox is not null;
}

/// <summary>A page of measurements plus the cursor for the following page (null when exhausted).</summary>
public sealed record ObservationQueryResult(
    IReadOnlyList<Measurement> Items,
    ObservationCursor? NextCursor);
