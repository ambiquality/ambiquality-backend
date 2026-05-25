using Ambiquality.Evidence.Api.Application.Abstractions;

namespace Ambiquality.Evidence.Api.Tests.TestSupport;

/// <summary>Hand-written <see cref="ICurrentUser"/> for handler unit tests.</summary>
public sealed class StubCurrentUser : ICurrentUser
{
    public StubCurrentUser(Guid authUserId, Guid projectionId)
    {
        AuthUserId = authUserId;
        ProjectionId = projectionId;
    }

    public bool IsAuthenticated => true;
    public Guid AuthUserId { get; }
    public Guid ProjectionId { get; }
}
