namespace Ambiquality.Auth.Api.Application.Abstractions;

/// <summary>Abstraction over the system clock so expiry logic is testable.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
