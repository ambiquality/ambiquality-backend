namespace Ambiquality.Observability.Tests;

/// <summary>
/// Window semantics of the distinct-entity activity tracker that feeds
/// <c>ambiquality.active_users</c> (5m/1h/24h) and, indirectly, any other gauge built
/// on <see cref="RollingActivityGauge"/>.
/// </summary>
public class RollingActivityGaugeTests
{
    private static RollingActivityGauge New() => new(TimeSpan.FromHours(24));

    [Fact]
    public void Counts_EachKeyOnce_PerWindow()
    {
        var now = 1_700_000_000L;
        var gauge = New();

        gauge.Record("a", now);
        gauge.Record("a", now + 1);
        gauge.Record("a", now + 2);

        Assert.Equal(1, gauge.CountInWindow(TimeSpan.FromMinutes(5), now + 2));
    }

    [Fact]
    public void Keys_RefreshedIntoWindow_AreCounted()
    {
        var now = 1_700_000_000L;
        var gauge = New();

        // "c" was active minutes ago AND again just now → counted once in every window.
        gauge.Record("a", now);
        gauge.Record("b", now - 60);
        gauge.Record("c", now - 2 * 60 * 60);
        gauge.Record("c", now);

        Assert.Equal(3, gauge.CountInWindow(TimeSpan.FromMinutes(5), now));
        Assert.Equal(3, gauge.CountInWindow(TimeSpan.FromHours(1), now));
        Assert.Equal(3, gauge.CountInWindow(TimeSpan.FromHours(24), now));
    }

    [Fact]
    public void Keys_OlderThanWindow_AreExcluded()
    {
        var now = 1_700_000_000L;
        var gauge = New();

        gauge.Record("recent", now);
        gauge.Record("stale-5m", now - (5 * 60 + 1));
        gauge.Record("stale-1h", now - (60 * 60 + 1));

        Assert.Equal(1, gauge.CountInWindow(TimeSpan.FromMinutes(5), now));
        Assert.Equal(2, gauge.CountInWindow(TimeSpan.FromHours(1), now));
        Assert.Equal(3, gauge.CountInWindow(TimeSpan.FromHours(24), now));
    }

    [Fact]
    public void Prune_Removes_EntriesOlderThanMaxWindow()
    {
        var now = 1_700_000_000L;
        var gauge = New(); // max window 24h

        gauge.Record("fresh", now);
        gauge.Record("ancient", now - 25 * 60 * 60);

        gauge.Prune(now);

        Assert.Equal(1, gauge.CountInWindow(TimeSpan.FromHours(24), now));
    }

    [Fact]
    public void Empty_TrackerCountsZero()
    {
        var gauge = New();
        Assert.Equal(0, gauge.CountInWindow(TimeSpan.FromMinutes(5), 1_700_000_000L));
    }
}
