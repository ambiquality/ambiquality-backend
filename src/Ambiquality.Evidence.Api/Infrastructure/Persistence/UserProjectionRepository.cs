using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence;

public sealed class UserProjectionRepository(EvidenceDbContext context) : IUserProjectionRepository
{
    public async Task<Guid> FindOrCreateAsync(Guid authUserId, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await context.UserProjections
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.AuthUserId == authUserId, cancellationToken);
        if (existing is not null)
            return existing.Id;

        var projection = new UserProjection(authUserId, now);
        context.UserProjections.Add(projection);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return projection.Id;
        }
        catch (DbUpdateException)
        {
            // A concurrent first request won the race; the unique index rejected
            // ours. Drop our attempt and read the row that landed.
            context.Entry(projection).State = EntityState.Detached;
            var winner = await context.UserProjections
                .AsNoTracking()
                .FirstAsync(u => u.AuthUserId == authUserId, cancellationToken);
            return winner.Id;
        }
    }
}
