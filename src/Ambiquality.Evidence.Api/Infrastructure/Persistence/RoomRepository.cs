using Ambiquality.Evidence.Api.Application;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence;

public sealed class RoomRepository(EvidenceDbContext dbContext) : IRoomRepository
{
    private const string DuplicateSlugConstraint = "IX_room_uri_slug_unique";
    private const string OverlappingValidityConstraint = "room_*_history_no_overlapping_validity";

    public async Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Room?> GetBySlugAsync(Guid buildingId, UriSlug slug, CancellationToken cancellationToken = default)
    {
        return await dbContext.Rooms
            .FirstOrDefaultAsync(
                r => r.BuildingId == buildingId && r.UriSlug == slug.Value,
                cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(UriSlug slug, CancellationToken cancellationToken = default)
    {
        return await dbContext.Rooms.AnyAsync(r => r.UriSlug == slug.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<Room>> GetByBuildingIdAsync(Guid buildingId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Rooms
            .Where(r => r.BuildingId == buildingId)
            .ToListAsync(cancellationToken);
    }

    public void Add(Room room)
    {
        dbContext.Rooms.Add(room);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException is PostgresException pgEx)
            {
                // Check for duplicate URI slug within building
                if (pgEx.ConstraintName == DuplicateSlugConstraint)
                {
                    throw new DuplicateUriSlugException();
                }

                // Check for overlapping validity ranges (GiST exclusion constraint)
                if (pgEx.ConstraintName?.Contains("no_overlapping_validity") == true)
                {
                    throw new OverlappingValidityRangeException();
                }
            }

            throw;
        }
    }
}
