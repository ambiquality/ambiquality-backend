using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Common;

/// <summary>
/// Shared base for every attribute-level history row. Holds the half-open
/// <c>tstzrange</c> validity period plus the audit stamp (<see cref="RecordedAt"/>
/// / <see cref="RecordedBy"/>) and the <see cref="Close"/> transition, so the
/// concrete rows only declare their own foreign key and payload.
/// </summary>
public abstract class HistoryRow
{
    /// <summary>EF materialisation ctor.</summary>
    protected HistoryRow() { }

    protected HistoryRow(NpgsqlRange<DateTime> validity, Guid recordedBy, DateTime recordedAt)
    {
        Validity = validity;
        RecordedBy = recordedBy;
        // Truncate to microseconds in one place: timestamptz stores µs, so the
        // (id, recorded_at) composite key must round-trip identically. recordedAt
        // may come from the clock (registration) or the request's validFrom (a
        // change), so truncating here covers both — see review-followups plan 03 B2.
        RecordedAt = new DateTime(recordedAt.Ticks / 10 * 10, recordedAt.Kind);
    }

    public NpgsqlRange<DateTime> Validity { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public Guid RecordedBy { get; private set; }

    /// <summary>
    /// Closes this row at <paramref name="upper"/> (exclusive). Called by the
    /// aggregate when a newer value takes effect; the half-open <c>[lower, upper)</c>
    /// range keeps the closed row and the next open row from both containing the
    /// boundary instant.
    /// </summary>
    public void Close(DateTime upper) =>
        Validity = Common.Validity.Closed(Validity.LowerBound, upper);
}
