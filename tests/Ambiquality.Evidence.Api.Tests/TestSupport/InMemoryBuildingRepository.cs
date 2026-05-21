using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Tests.TestSupport;

/// <summary>Hand-written in-memory <see cref="IBuildingRepository"/> for handler tests.</summary>
public sealed class InMemoryBuildingRepository : IBuildingRepository
{
    private readonly List<Building> _buildings = [];

    public int SaveChangesCallCount { get; private set; }

    public IReadOnlyList<Building> Buildings => _buildings;

    public Task<Building?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_buildings.FirstOrDefault(b => b.Id == id));

    public Task<Building?> GetBySlugAsync(UriSlug slug, CancellationToken cancellationToken = default)
        => Task.FromResult(_buildings.FirstOrDefault(b => b.UriSlug == slug.Value));

    public Task<IReadOnlyList<Building>> ListOwnedByAsync(
        Guid userProjectionId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Building>>(
            _buildings.Where(b => b.OwnerId == userProjectionId).ToList());

    public void Add(Building building) => _buildings.Add(building);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
