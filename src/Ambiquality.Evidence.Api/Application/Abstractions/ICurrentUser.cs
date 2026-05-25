namespace Ambiquality.Evidence.Api.Application.Abstractions;

/// <summary>
/// Exposes the authenticated principal to handlers. <c>AuthUserId</c> is the
/// original GUID from the JWT <c>sub</c> claim; <c>ProjectionId</c> is the
/// evidence-side <see cref="Domain.Users.UserProjection"/> row id, resolved by
/// the current-user middleware. On anonymous requests (e.g. public reads)
/// <c>IsAuthenticated</c> is false and the id properties throw.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid AuthUserId { get; }

    Guid ProjectionId { get; }
}
