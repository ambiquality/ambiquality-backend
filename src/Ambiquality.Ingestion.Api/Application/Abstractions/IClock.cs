namespace Ambiquality.Ingestion.Api.Application.Abstractions;

/// <summary>Abstraction over the system clock so behavior tests are deterministic.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
