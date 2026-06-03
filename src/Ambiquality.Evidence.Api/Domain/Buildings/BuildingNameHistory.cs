using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>
/// Per-attribute history row for a building's display name. The open row
/// (upper-infinite validity) is the current value.
/// </summary>
public sealed class BuildingNameHistory : HistoryRow
{
    private BuildingNameHistory() { Name = null!; }

    internal BuildingNameHistory(string name, NpgsqlRange<DateTime> validity, DateTime recordedAt, Guid recordedBy)
        : base(validity, recordedBy, recordedAt)
    {
        Name = name;
    }

    public string Name { get; private set; }
}
