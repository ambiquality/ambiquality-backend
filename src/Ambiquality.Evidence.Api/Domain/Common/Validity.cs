using NpgsqlTypes;

namespace Ambiquality.Evidence.Api.Domain.Common;

/// <summary>
/// Sole factory for the <c>tstzrange</c> values that drive attribute-level
/// versioning. All ranges are half-open <c>[lower, upper)</c> with UTC
/// timestamps; this is the only legal way to construct a
/// <see cref="NpgsqlRange{T}"/> in the evidence domain.
/// </summary>
public static class Validity
{
    /// <summary>Builds an open-ended <c>[from, +∞)</c> range.</summary>
    public static NpgsqlRange<DateTime> OpenFrom(DateTime from)
    {
        EnsureUtc(from, nameof(from));
        return new NpgsqlRange<DateTime>(
            lowerBound: from,
            lowerBoundIsInclusive: true,
            lowerBoundInfinite: false,
            upperBound: default,
            upperBoundIsInclusive: false,
            upperBoundInfinite: true);
    }

    /// <summary>Builds a closed half-open <c>[from, to)</c> range.</summary>
    public static NpgsqlRange<DateTime> Closed(DateTime from, DateTime to)
    {
        EnsureUtc(from, nameof(from));
        EnsureUtc(to, nameof(to));
        if (from >= to)
            throw new ArgumentException(
                "Validity range lower bound must be strictly before upper bound.",
                nameof(from));

        return new NpgsqlRange<DateTime>(
            lowerBound: from,
            lowerBoundIsInclusive: true,
            lowerBoundInfinite: false,
            upperBound: to,
            upperBoundIsInclusive: false,
            upperBoundInfinite: false);
    }

    private static void EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException(
                "Validity timestamps must be UTC (DateTimeKind.Utc).",
                paramName);
    }
}
