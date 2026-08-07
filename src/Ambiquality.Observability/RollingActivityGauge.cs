using System.Collections.Concurrent;

namespace Ambiquality.Observability;

/// <summary>
/// Tracks the last-seen timestamp of distinct keys (e.g. operator auth user ids)
/// so a "how many distinct entities were active in the last N minutes" count can be
/// derived cheaply for several rolling windows from a single store. Entries older
/// than the configured maximum window are pruned.
/// </summary>
public sealed class RollingActivityGauge
{
    private readonly ConcurrentDictionary<string, long> _lastSeen = new();
    private readonly long _maxWindowSeconds;

    public RollingActivityGauge(TimeSpan maxWindow)
    {
        _maxWindowSeconds = (long)maxWindow.TotalSeconds;
    }

    /// <summary>Records activity for <paramref name="key"/> at <paramref name="unixSeconds"/>.</summary>
    public void Record(string key, long unixSeconds) => _lastSeen[key] = unixSeconds;

    /// <summary>Count of distinct keys with activity within the given window.</summary>
    public int CountInWindow(TimeSpan window, long unixNow)
    {
        var cutoff = unixNow - (long)window.TotalSeconds;
        var count = 0;
        foreach (var lastSeen in _lastSeen.Values)
        {
            if (lastSeen >= cutoff) count++;
        }
        return count;
    }

    /// <summary>Drops entries idle for longer than the configured maximum window.</summary>
    public void Prune(long unixNow)
    {
        var cutoff = unixNow - _maxWindowSeconds;
        foreach (var entry in _lastSeen)
        {
            if (entry.Value < cutoff)
                _lastSeen.TryRemove(entry.Key, out _);
        }
    }
}
