using Ambiquality.Evidence.Api.Application.Abstractions;

namespace Ambiquality.Evidence.Api.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
