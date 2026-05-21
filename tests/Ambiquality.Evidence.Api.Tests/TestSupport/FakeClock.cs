using Ambiquality.Evidence.Api.Application.Abstractions;

namespace Ambiquality.Evidence.Api.Tests.TestSupport;

/// <summary>Deterministic clock for handler tests.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; set; }
}
