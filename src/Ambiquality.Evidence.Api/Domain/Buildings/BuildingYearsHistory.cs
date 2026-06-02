using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>Per-attribute history row for a building's construction / renovation years.</summary>
public sealed class BuildingYearsHistory : HistoryRow
{
    private BuildingYearsHistory() { }

    internal BuildingYearsHistory(short? yearBuilt, short? yearRenovated, NpgsqlRange<DateTime> validity, DateTime recordedAt, Guid recordedBy)
        : base(validity, recordedBy, recordedAt)
    {
        YearBuilt = yearBuilt;
        YearRenovated = yearRenovated;
    }

    public short? YearBuilt { get; private set; }
    public short? YearRenovated { get; private set; }
}
