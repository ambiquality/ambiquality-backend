namespace Ambiquality.Evidence.Api.Application.Abstractions;

/// <summary>
/// Resolves the local <see cref="Domain.Users.UserProjection"/> id for an
/// authenticated user, creating the row on first sight (lazy upsert).
/// </summary>
public interface IUserProjectionRepository
{
    Task<Guid> FindOrCreateAsync(Guid authUserId, DateTime now, CancellationToken cancellationToken);
}
