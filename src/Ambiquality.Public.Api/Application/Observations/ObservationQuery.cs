using Ambiquality.Core.Domain.Measurements;
using Ambiquality.Core.Infrastructure.Persistence;
using Ambiquality.Public.Api.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Ambiquality.Public.Api.Application.Observations;

/// <summary>
/// Builds the keyset-paginated observation query over the <c>measurements</c>
/// hypertable. Keyset paging on <c>(received_at DESC, id DESC)</c> lets TimescaleDB
/// prune chunks and avoids the OFFSET cost that grows with page depth.
/// </summary>
public static class ObservationQuery
{
    /// <summary>Fetches one keyset page; the result carries the next-page cursor when more rows remain.</summary>
    public static async Task<ObservationQueryResult> PageAsync(
        IeqDbContext context, IEvidenceCatalog catalog, ObservationFilter filter, CancellationToken ct)
    {
        // building/room/bbox filters resolve to a set of sensor ids; an empty set
        // means nothing can match, so short-circuit with an empty page.
        List<Guid>? sensorIds = null;
        if (filter.NeedsSensorResolution)
        {
            var resolved = await catalog.ResolveSensorIdsAsync(filter.BuildingId, filter.RoomId, filter.Bbox, ct);
            if (resolved.Count == 0)
                return new ObservationQueryResult([], null);
            sensorIds = resolved.ToList();
        }

        var query = Apply(context.Measurements.AsNoTracking(), filter, sensorIds);

        if (filter.Cursor is { } cursor)
        {
            // OR form — EF/Npgsql cannot translate the SQL row-value comparator
            // (received_at, id) < (x, y). Guid has no '<' operator in C#, so the
            // tie-break uses CompareTo, which Npgsql renders as a uuid comparison.
            query = query.Where(m =>
                m.ReceivedAt < cursor.ReceivedAt
                || (m.ReceivedAt == cursor.ReceivedAt && m.Id.CompareTo(cursor.Id) < 0));
        }

        var rows = await query
            .OrderByDescending(m => m.ReceivedAt)
            .ThenByDescending(m => m.Id)
            .Take(filter.Limit + 1)
            .ToListAsync(ct);

        ObservationCursor? next = null;
        if (rows.Count > filter.Limit)
        {
            rows.RemoveAt(rows.Count - 1);
            var last = rows[^1];
            next = new ObservationCursor(last.ReceivedAt, last.Id);
        }

        return new ObservationQueryResult(rows, next);
    }

    /// <summary>
    /// Streams every measurement matching the filter in keyset order, for the CSV
    /// export. Backed by the EF data reader (<see cref="EntityFrameworkQueryableExtensions.AsAsyncEnumerable{T}"/>),
    /// so memory stays bounded regardless of result size. Returns null when a
    /// building/room/bbox filter resolves to no sensors.
    /// </summary>
    public static async Task<IAsyncEnumerable<Measurement>?> StreamAsync(
        IeqDbContext context, IEvidenceCatalog catalog, ObservationFilter filter, CancellationToken ct)
    {
        List<Guid>? sensorIds = null;
        if (filter.NeedsSensorResolution)
        {
            var resolved = await catalog.ResolveSensorIdsAsync(filter.BuildingId, filter.RoomId, filter.Bbox, ct);
            if (resolved.Count == 0)
                return null;
            sensorIds = resolved.ToList();
        }

        return Apply(context.Measurements.AsNoTracking(), filter, sensorIds)
            .OrderByDescending(m => m.ReceivedAt)
            .ThenByDescending(m => m.Id)
            .AsAsyncEnumerable();
    }

    /// <summary>Fetches a single measurement by id (id alone is selective; the PK is composite).</summary>
    public static Task<Measurement?> GetByIdAsync(IeqDbContext context, Guid id, CancellationToken ct) =>
        context.Measurements.AsNoTracking().Where(m => m.Id == id).FirstOrDefaultAsync(ct);

    private static IQueryable<Measurement> Apply(
        IQueryable<Measurement> query, ObservationFilter filter, List<Guid>? sensorIds)
    {
        if (!filter.IncludeInvalid)
            query = query.Where(m => !m.IsInvalid);
        if (filter.From is { } from)
            query = query.Where(m => m.ReceivedAt >= from);
        if (filter.To is { } to)
            query = query.Where(m => m.ReceivedAt <= to);
        if (filter.SensorId is { } sensorId)
            query = query.Where(m => m.SensorId == sensorId);
        if (filter.ParameterCode is { } parameterCode)
            query = query.Where(m => m.ParameterCode == parameterCode);
        if (sensorIds is not null)
            query = query.Where(m => sensorIds.Contains(m.SensorId));

        return query;
    }
}
