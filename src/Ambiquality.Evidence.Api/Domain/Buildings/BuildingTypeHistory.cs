using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>Per-attribute history row for a building's type codelist reference.</summary>
public sealed class BuildingTypeHistory
{
    private BuildingTypeHistory() { BuildingTypeCode = null!; }

    internal BuildingTypeHistory(string buildingTypeCode, NpgsqlRange<DateTime> validity, DateTime recordedAt, Guid recordedBy)
    {
        Id = Guid.NewGuid();
        BuildingTypeCode = buildingTypeCode;
        Validity = validity;
        RecordedAt = recordedAt;
        RecordedBy = recordedBy;
    }

    public Guid Id { get; private set; }
    public string BuildingTypeCode { get; private set; }
    public NpgsqlRange<DateTime> Validity { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public Guid RecordedBy { get; private set; }

    internal void Close(DateTime upper)
    {
        Validity = Common.Validity.Closed(Validity.LowerBound, upper);
    }
}
