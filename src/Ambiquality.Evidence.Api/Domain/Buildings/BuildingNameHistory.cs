using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>
/// Per-attribute history row for a building's display name. Each row owns a
/// half-open <c>tstzrange</c>; the open row (upper-infinite) is the current
/// value.
/// </summary>
public sealed class BuildingNameHistory
{
    private BuildingNameHistory() { Name = null!; }

    internal BuildingNameHistory(string name, NpgsqlRange<DateTime> validity, DateTime recordedAt, Guid recordedBy)
    {
        Name = name;
        Validity = validity;
        RecordedAt = new DateTime(recordedAt.Ticks / 10 * 10, recordedAt.Kind);
        RecordedBy = recordedBy;
    }

    public string Name { get; private set; }
    public NpgsqlRange<DateTime> Validity { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public Guid RecordedBy { get; private set; }

    /// <summary>
    /// Closes this row at <paramref name="upper"/>; called by the aggregate when
    /// a newer value takes effect.
    /// </summary>
    internal void Close(DateTime upper)
    {
        Validity = Common.Validity.Closed(Validity.LowerBound, upper);
    }
}
