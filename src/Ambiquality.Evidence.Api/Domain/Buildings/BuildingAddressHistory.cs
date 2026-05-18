using Ambiquality.Evidence.Api.Domain.Common;
using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>Per-attribute history row for a building's postal address.</summary>
public sealed class BuildingAddressHistory
{
    private BuildingAddressHistory() { Address = null!; }

    internal BuildingAddressHistory(Address address, NpgsqlRange<DateTime> validity, DateTime recordedAt, Guid recordedBy)
    {
        Id = Guid.NewGuid();
        Address = address;
        Validity = validity;
        RecordedAt = recordedAt;
        RecordedBy = recordedBy;
    }

    public Guid Id { get; private set; }
    public Address Address { get; private set; }
    public NpgsqlRange<DateTime> Validity { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public Guid RecordedBy { get; private set; }

    internal void Close(DateTime upper)
    {
        Validity = Common.Validity.Closed(Validity.LowerBound, upper);
    }
}
