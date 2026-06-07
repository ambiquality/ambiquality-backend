extern alias PublicApi;
using PublicApi::Ambiquality.Public.Api.Application.Observations;

namespace Ambiquality.Public.Api.Tests;

/// <summary>Pure unit tests for the auto bucket selection + cap (no DB).</summary>
public sealed class AggregateBucketTests
{
    private static readonly DateTime Origin = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("5m", "5m")]
    [InlineData("1h", "1h")]
    [InlineData("1w", "1w")]
    [InlineData("AUTO", null)] // case-insensitive auto resolves without error
    public void TryResolve_KnownLabels_Succeed(string raw, string? expectedLabelOrAuto)
    {
        var ok = AggregateBucket.TryResolve(raw, Origin, Origin.AddDays(1), out var bucket);

        Assert.True(ok);
        if (expectedLabelOrAuto is not null)
            Assert.Equal(expectedLabelOrAuto, bucket.Label);
    }

    [Fact]
    public void TryResolve_UnknownLabel_Fails()
    {
        Assert.False(AggregateBucket.TryResolve("17s", Origin, Origin.AddDays(1), out _));
    }

    [Fact]
    public void Auto_OverADay_PicksFineGranularity()
    {
        AggregateBucket.TryResolve("auto", Origin, Origin.AddDays(1), out var bucket);
        // 1 day / 5 min = 288 buckets ≤ 500 → the finest ladder rung is chosen.
        Assert.Equal("5m", bucket.Label);
    }

    [Fact]
    public void Auto_OverAYear_CoarsensToStayUnderCap()
    {
        AggregateBucket.TryResolve("auto", Origin, Origin.AddYears(1), out var bucket);
        // A year in 5-min/1-h/6-h buckets all exceed 500; 1 day = 365 ≤ 500.
        Assert.Equal("1d", bucket.Label);
    }

    [Fact]
    public void ExplicitBucket_BelowCap_IsCoarsenedUp()
    {
        // Asking for 5-minute buckets across a year would be ~105k buckets; the resolver
        // honours the floor but climbs the ladder until the count fits the cap.
        AggregateBucket.TryResolve("5m", Origin, Origin.AddYears(1), out var bucket);
        Assert.Equal("1d", bucket.Label);
    }
}
