using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Domain.Rooms;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Room?> GetBySlugAsync(Guid buildingId, UriSlug slug, CancellationToken ct = default);
    Task<IReadOnlyList<Room>> GetByBuildingIdAsync(Guid buildingId, CancellationToken ct = default);
    void Add(Room room);
    Task SaveChangesAsync(CancellationToken ct = default);
}
