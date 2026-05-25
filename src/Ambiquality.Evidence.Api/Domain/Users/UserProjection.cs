namespace Ambiquality.Evidence.Api.Domain.Users;

/// <summary>
/// Evidence-side projection of an authenticated user. The catalog stores ownership
/// and audit columns against this local <see cref="Id"/> rather than the raw auth
/// <c>sub</c> GUID, keeping a stable evidence identity even though there is no
/// cross-database FK to the auth service. Rows are created lazily on first
/// authenticated request, keyed by the unique <see cref="AuthUserId"/>.
/// </summary>
public sealed class UserProjection
{
    private UserProjection() { }

    public UserProjection(Guid authUserId, DateTime createdAt)
    {
        Id = Guid.NewGuid();
        AuthUserId = authUserId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>The auth service's user GUID, taken from the JWT <c>sub</c> claim.</summary>
    public Guid AuthUserId { get; private set; }

    public DateTime CreatedAt { get; private set; }
}
