using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>Per-attribute history row for a building's type codelist reference.</summary>
public sealed class BuildingTypeHistory : HistoryRow
{
    private BuildingTypeHistory() { BuildingTypeCode = null!; }

    internal BuildingTypeHistory(string buildingTypeCode, NpgsqlRange<DateTime> validity, DateTime recordedAt, Guid recordedBy)
        : base(validity, recordedBy, recordedAt)
    {
        BuildingTypeCode = buildingTypeCode;
    }

    public string BuildingTypeCode { get; private set; }
}
