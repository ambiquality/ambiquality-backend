namespace Ambiquality.Evidence.Api.Domain.Rooms;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Room?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IAsyncEnumerable<Room>> ListByBuildingAsync(Guid buildingId, CancellationToken ct = default);
    void Add(Room room);
    Task SaveChangesAsync(CancellationToken ct = default);
}
