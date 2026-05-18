using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>
/// Per-attribute history row for a building's spatial location: the precise
/// coordinates (optional) co-vary with the anonymization level that controls
/// public exposure.
/// </summary>
public sealed class BuildingLocationHistory
{
    private BuildingLocationHistory() { Anonymization = null!; }

    internal BuildingLocationHistory(
        Coordinates? coordinates,
        AnonymizationLevel anonymization,
        NpgsqlRange<DateTime> validity,
        DateTime recordedAt,
        Guid recordedBy)
    {
        Id = Guid.NewGuid();
        Coordinates = coordinates;
        Anonymization = anonymization;
        Validity = validity;
        RecordedAt = recordedAt;
        RecordedBy = recordedBy;
    }

    public Guid Id { get; private set; }
    public Coordinates? Coordinates { get; private set; }
    public AnonymizationLevel Anonymization { get; private set; }
    public NpgsqlRange<DateTime> Validity { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public Guid RecordedBy { get; private set; }

    internal void Close(DateTime upper)
    {
        Validity = Common.Validity.Closed(Validity.LowerBound, upper);
    }
}
