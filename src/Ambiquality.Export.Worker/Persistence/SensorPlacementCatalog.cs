using Ambiquality.Export.Worker.Serialization;
using Npgsql;

namespace Ambiquality.Export.Worker.Persistence;

/// <summary>Loads the sensor placement history into a feature-of-interest resolver.</summary>
public interface ISensorPlacementCatalog
{
    Task<FeatureOfInterestResolver> LoadResolverAsync(CancellationToken ct);
}

/// <summary>
/// Reads the sensor placement history from the read-only evidence database to build a
/// <see cref="FeatureOfInterestResolver"/>. The sensor registry is the small, bounded
/// device catalog, so the whole placement history is loaded once per export and resolved
/// in memory — no per-row cross-database query.
/// </summary>
public sealed class SensorPlacementCatalog(EvidenceDataSource evidence) : ISensorPlacementCatalog
{
    public async Task<FeatureOfInterestResolver> LoadResolverAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT sensor_id, room_id, lower(validity), upper(validity)
            FROM evidence.sensor_placement_history
            """;

        await using var connection = await evidence.Source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);

        var placements = new List<SensorPlacement>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            placements.Add(new SensorPlacement(
                SensorId: reader.GetGuid(0),
                RoomId: reader.GetGuid(1),
                ValidFrom: reader.GetDateTime(2),
                ValidTo: reader.IsDBNull(3) ? null : reader.GetDateTime(3)));

        return new FeatureOfInterestResolver(placements);
    }
}
