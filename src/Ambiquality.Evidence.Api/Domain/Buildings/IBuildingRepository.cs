using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Domain.Buildings;

/// <summary>
/// Persistence port for the <see cref="Building"/> aggregate. The
/// implementation lives in the Infrastructure layer.
/// </summary>
public interface IBuildingRepository
{
    Task<Building?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Building?> GetBySlugAsync(UriSlug slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Building>> ListOwnedByAsync(
        Guid userProjectionId, CancellationToken cancellationToken = default);

    void Add(Building building);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
