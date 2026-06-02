namespace Ambiquality.Auth.Api.Application.Abstractions;

/// <summary>
/// Introduces a real-time delay. Abstracted so login throttling can be unit-tested
/// without actually sleeping.
/// </summary>
public interface IThrottleDelayer
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}
