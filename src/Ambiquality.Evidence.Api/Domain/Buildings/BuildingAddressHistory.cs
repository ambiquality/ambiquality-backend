using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>Per-attribute history row for a building's postal address.</summary>
public sealed class BuildingAddressHistory
{
    private BuildingAddressHistory() { Address = null!; }

    internal BuildingAddressHistory(Address address, NpgsqlRange<DateTime> validity, DateTime recordedAt, Guid recordedBy)
    {
        Address = address;
        Validity = validity;
        RecordedAt = new DateTime(recordedAt.Ticks / 10 * 10, recordedAt.Kind);
        RecordedBy = recordedBy;
    }

    public Address Address { get; private set; }
    public NpgsqlRange<DateTime> Validity { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public Guid RecordedBy { get; private set; }

    internal void Close(DateTime upper)
    {
        Validity = Common.Validity.Closed(Validity.LowerBound, upper);
    }
}
