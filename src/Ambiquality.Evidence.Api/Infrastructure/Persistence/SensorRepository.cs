using Ambiquality.Evidence.Api.Application;
using Ambiquality.Evidence.Api.Domain.Common;
using Ambiquality.Evidence.Api.Domain.Sensors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence;

public sealed class SensorRepository(EvidenceDbContext dbContext) : ISensorRepository
{
    private const string DuplicateSlugConstraint = "IX_sensor_uri_slug_unique";

    public async Task<Sensor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Sensors.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Sensor?> GetBySlugAsync(UriSlug slug, CancellationToken cancellationToken = default)
    {
        return await dbContext.Sensors
            .FirstOrDefaultAsync(s => s.UriSlug == slug.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<Sensor>> GetByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Sensors
            .Where(s => s.CurrentRoomId == roomId)
            .ToListAsync(cancellationToken);
    }

    public void Add(Sensor sensor)
    {
        dbContext.Sensors.Add(sensor);
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
                if (pgEx.ConstraintName == DuplicateSlugConstraint)
                {
                    throw new DuplicateUriSlugException();
                }

                if (pgEx.ConstraintName?.Contains("no_overlapping_validity") == true)
                {
                    throw new OverlappingValidityRangeException();
                }
            }

            throw;
        }
    }
}
