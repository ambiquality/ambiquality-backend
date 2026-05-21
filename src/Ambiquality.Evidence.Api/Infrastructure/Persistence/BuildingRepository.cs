using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence;

public sealed class BuildingRepository(EvidenceDbContext dbContext) : IBuildingRepository
{
    public async Task<Building?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Buildings.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Building?> GetBySlugAsync(UriSlug slug, CancellationToken cancellationToken = default)
    {
        return await dbContext.Buildings.FirstOrDefaultAsync(b => b.UriSlug == slug.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<Building>> ListOwnedByAsync(
        Guid userProjectionId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Buildings
            .Where(b => b.OwnerId == userProjectionId)
            .ToListAsync(cancellationToken);
    }

    public void Add(Building building)
    {
        dbContext.Buildings.Add(building);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
