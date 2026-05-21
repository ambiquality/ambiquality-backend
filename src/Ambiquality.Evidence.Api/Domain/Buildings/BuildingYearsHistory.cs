using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>Per-attribute history row for a building's construction / renovation years.</summary>
public sealed class BuildingYearsHistory
{
    private BuildingYearsHistory() { }

    internal BuildingYearsHistory(short? yearBuilt, short? yearRenovated, NpgsqlRange<DateTime> validity, DateTime recordedAt, Guid recordedBy)
    {
        YearBuilt = yearBuilt;
        YearRenovated = yearRenovated;
        Validity = validity;
        RecordedAt = recordedAt;
        RecordedBy = recordedBy;
    }

    public short? YearBuilt { get; private set; }
    public short? YearRenovated { get; private set; }
    public NpgsqlRange<DateTime> Validity { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public Guid RecordedBy { get; private set; }

    internal void Close(DateTime upper)
    {
        Validity = Common.Validity.Closed(Validity.LowerBound, upper);
    }
}
