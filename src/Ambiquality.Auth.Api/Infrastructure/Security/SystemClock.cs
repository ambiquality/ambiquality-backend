using Ambiquality.Auth.Api.Application.Abstractions;

namespace Ambiquality.Auth.Api.Infrastructure.Security;

/// <summary>Production <see cref="IClock"/> backed by the system UTC clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
