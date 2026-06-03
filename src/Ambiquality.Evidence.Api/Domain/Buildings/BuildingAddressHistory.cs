using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>Per-attribute history row for a building's postal address.</summary>
public sealed class BuildingAddressHistory : HistoryRow
{
    private BuildingAddressHistory() { Address = null!; }

    internal BuildingAddressHistory(Address address, NpgsqlRange<DateTime> validity, DateTime recordedAt, Guid recordedBy)
        : base(validity, recordedBy, recordedAt)
    {
        Address = address;
    }

    public Address Address { get; private set; }
}
