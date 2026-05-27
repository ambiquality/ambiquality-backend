using Ambiquality.Ingestion.Api.Application.Abstractions;

namespace Ambiquality.Ingestion.Api.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
