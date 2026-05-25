using Ambiquality.Evidence.Api.Application.Abstractions;

namespace Ambiquality.Evidence.Api.Infrastructure.Security;

/// <summary>
/// Per-request principal. Starts anonymous; <see cref="CurrentUserMiddleware"/>
/// promotes it to authenticated once the JWT is validated and the user's
/// projection row is resolved.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private Guid? _authUserId;
    private Guid? _projectionId;

    public bool IsAuthenticated => _authUserId.HasValue;

    public Guid AuthUserId => _authUserId
        ?? throw new InvalidOperationException("The current request has no authenticated user.");

    public Guid ProjectionId => _projectionId
        ?? throw new InvalidOperationException("The current request has no authenticated user.");

    internal void SetAuthenticated(Guid authUserId, Guid projectionId)
    {
        _authUserId = authUserId;
        _projectionId = projectionId;
    }
}
