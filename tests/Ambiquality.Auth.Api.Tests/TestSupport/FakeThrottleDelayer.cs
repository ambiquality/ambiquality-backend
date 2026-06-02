using Ambiquality.Auth.Api.Application.Abstractions;

namespace Ambiquality.Auth.Api.Tests.TestSupport;

/// <summary>
/// Records requested throttle delays instead of actually sleeping, so login
/// backoff can be asserted in fast unit tests.
/// </summary>
public sealed class FakeThrottleDelayer : IThrottleDelayer
{
    public List<TimeSpan> Delays { get; } = [];

    public TimeSpan LastDelay => Delays.Count > 0 ? Delays[^1] : TimeSpan.Zero;

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        Delays.Add(delay);
        return Task.CompletedTask;
    }
}
