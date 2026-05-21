using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Domain.Sensors;

public interface ISensorRepository
{
    Task<Sensor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Sensor?> GetBySlugAsync(UriSlug slug, CancellationToken ct = default);
    Task<IReadOnlyList<Sensor>> GetByRoomIdAsync(Guid roomId, CancellationToken ct = default);
    void Add(Sensor sensor);
    Task SaveChangesAsync(CancellationToken ct = default);
}
