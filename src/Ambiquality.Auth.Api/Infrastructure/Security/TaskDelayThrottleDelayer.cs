using Ambiquality.Auth.Api.Application.Abstractions;

namespace Ambiquality.Auth.Api.Infrastructure.Security;

/// <summary>Production <see cref="IThrottleDelayer"/> backed by <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
public sealed class TaskDelayThrottleDelayer : IThrottleDelayer
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        => Task.Delay(delay, cancellationToken);
}
