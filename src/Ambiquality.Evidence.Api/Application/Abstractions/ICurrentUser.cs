namespace Ambiquality.Evidence.Api.Application.Abstractions;

/// <summary>
/// Exposes the authenticated principal's identifiers to handlers:
/// <c>AuthUserId</c> is the original UUID from the JWT <c>sub</c> claim,
/// <c>ProjectionId</c> is the evidence-side <see cref="Domain.Users.UserProjection"/>
/// row id created by the lazy upsert middleware.
/// </summary>
public interface ICurrentUser
{
    Guid AuthUserId { get; }

    Guid ProjectionId { get; }
}
