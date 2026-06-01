using Ambiquality.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ambiquality.Public.Api.Application.Catalog;

/// <summary>Aggregate metadata about the published dataset, for the DCAT-AP catalog.</summary>
public static class CatalogMetadataQuery
{
    /// <summary>
    /// The temporal coverage of the measurements (min/max <c>received_at</c>).
    /// Both are null when no measurements exist yet.
    /// </summary>
    public static async Task<(DateTime? Start, DateTime? End)> GetTemporalExtentAsync(
        IeqDbContext context, CancellationToken ct)
    {
        var start = await context.Measurements.AsNoTracking()
            .MinAsync(m => (DateTime?)m.ReceivedAt, ct);
        var end = await context.Measurements.AsNoTracking()
            .MaxAsync(m => (DateTime?)m.ReceivedAt, ct);
        return (start, end);
    }
}
