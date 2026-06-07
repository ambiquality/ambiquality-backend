namespace Ambiquality.Public.Api.Application.Observations;

/// <summary>
/// A time-bucket granularity for the aggregation endpoint: its client-facing
/// <see cref="Label"/> (e.g. <c>1h</c>) and the Postgres <c>interval</c> literal passed
/// to TimescaleDB's <c>time_bucket</c> (e.g. <c>1 hour</c>).
/// </summary>
public sealed record AggregateBucket(string Label, string Interval, TimeSpan Span)
{
    /// <summary>The fixed ladder of selectable granularities, coarsest last.</summary>
    public static readonly IReadOnlyList<AggregateBucket> Ladder =
    [
        new("5m", "5 minutes", TimeSpan.FromMinutes(5)),
        new("1h", "1 hour", TimeSpan.FromHours(1)),
        new("6h", "6 hours", TimeSpan.FromHours(6)),
        new("1d", "1 day", TimeSpan.FromDays(1)),
        new("1w", "1 week", TimeSpan.FromDays(7))
    ];

    /// <summary>
    /// Caps the number of buckets a single response may carry, so an unbounded
    /// <c>from..to</c> span can never produce an unbounded result (the whole feature's
    /// "no lag" goal). Sits at the upper end of the contract's ~300–500 guidance.
    /// </summary>
    public const int MaxBuckets = 500;

    /// <summary>
    /// Resolves the requested <paramref name="raw"/> bucket parameter against the
    /// <paramref name="from"/>..<paramref name="to"/> span. <c>null</c>/empty/<c>auto</c>
    /// picks the finest ladder granularity whose bucket count stays within
    /// <see cref="MaxBuckets"/>; an explicit label is honoured but still coarsened up the
    /// ladder if it would exceed the cap. Returns <c>false</c> for an unknown label.
    /// </summary>
    public static bool TryResolve(string? raw, DateTime from, DateTime to, out AggregateBucket bucket)
    {
        var span = to > from ? to - from : TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            bucket = SmallestWithinCap(span, startIndex: 0);
            return true;
        }

        var index = IndexOfLabel(raw);
        if (index < 0)
        {
            bucket = null!;
            return false;
        }

        // Honour the explicit choice, but never below the cap: coarsen from there up.
        bucket = SmallestWithinCap(span, index);
        return true;
    }

    private static int IndexOfLabel(string label)
    {
        for (var i = 0; i < Ladder.Count; i++)
        {
            if (Ladder[i].Label.Equals(label, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static AggregateBucket SmallestWithinCap(TimeSpan span, int startIndex)
    {
        for (var i = startIndex; i < Ladder.Count; i++)
        {
            if (span <= TimeSpan.Zero || span.Ticks / Ladder[i].Span.Ticks <= MaxBuckets)
                return Ladder[i];
        }
        return Ladder[^1];
    }
}
