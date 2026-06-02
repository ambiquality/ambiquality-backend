using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>
/// Per-attribute history row for a building's spatial location: the precise
/// coordinates (optional) co-vary with the anonymization level that controls
/// public exposure.
/// </summary>
public sealed class BuildingLocationHistory : HistoryRow
{
    private BuildingLocationHistory() { Anonymization = null!; }

    internal BuildingLocationHistory(
        Coordinates? coordinates,
        AnonymizationLevel anonymization,
        NpgsqlRange<DateTime> validity,
        DateTime recordedAt,
        Guid recordedBy)
        : base(validity, recordedBy, recordedAt)
    {
        Coordinates = coordinates;
        Anonymization = anonymization;
    }

    public Coordinates? Coordinates { get; private set; }
    public AnonymizationLevel Anonymization { get; private set; }
}
