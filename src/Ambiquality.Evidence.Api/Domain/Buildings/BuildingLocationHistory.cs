using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>
/// Per-attribute history row for a building's spatial location: the precise
/// WGS-84 coordinates. Optional — only the building's municipality may be
/// known, in which case no point is recorded.
/// </summary>
public sealed class BuildingLocationHistory : HistoryRow
{
    private BuildingLocationHistory() { }

    internal BuildingLocationHistory(
        Coordinates? coordinates,
        NpgsqlRange<DateTime> validity,
        DateTime recordedAt,
        Guid recordedBy)
        : base(validity, recordedBy, recordedAt)
    {
        Coordinates = coordinates;
    }

    public Coordinates? Coordinates { get; private set; }
}
