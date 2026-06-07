using Ambiquality.Public.Api.Api;
using Npgsql;
using NpgsqlTypes;

namespace Ambiquality.Public.Api.Infrastructure.Observations;

/// <inheritdoc cref="IMeasurementReader"/>
public sealed class MeasurementReader(NpgsqlDataSource dataSource) : IMeasurementReader
{
    public async Task<IReadOnlyList<LatestObservation>> GetLatestPerSensorAsync(
        IReadOnlyCollection<Guid> sensorIds, string parameterCode, CancellationToken ct)
    {
        if (sensorIds.Count == 0)
            return [];

        // One row per sensor: its freshest valid observation of the quantity. DISTINCT ON
        // collapses to the first row of each sensor group in the ORDER BY, mirroring the
        // (received_at DESC, id DESC) keyset order the observations feed uses.
        const string sql = """
            SELECT DISTINCT ON (sensor_id) sensor_id, value, unit, observed_at, received_at
            FROM ieq.measurements
            WHERE sensor_id = ANY(@ids) AND parameter_code = @pc AND is_invalid = false
            ORDER BY sensor_id, received_at DESC, id DESC
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        AddIds(command, sensorIds);
        command.Parameters.Add(new NpgsqlParameter("pc", NpgsqlDbType.Text) { Value = parameterCode });

        var rows = new List<LatestObservation>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new LatestObservation(
                SensorId: reader.GetGuid(0),
                Value: reader.GetDouble(1),
                Unit: reader.IsDBNull(2) ? null : reader.GetString(2),
                ObservedAt: reader.GetDateTime(3),
                ReceivedAt: reader.GetDateTime(4)));
        }
        return rows;
    }

    public async Task<AggregateResult> AggregateAsync(
        IReadOnlyCollection<Guid> sensorIds, string parameterCode, DateTime from, DateTime to,
        string intervalLiteral, CancellationToken ct)
    {
        if (sensorIds.Count == 0)
            return new AggregateResult([], null, null);

        // Two queries over one connection: the bucketed series (trend) and the overall
        // distribution (boxplot). The WHERE clause is identical, so they describe the same
        // window. percentile_cont interpolates continuous quantiles; time_bucket aligns
        // buckets to the interval and lets TimescaleDB prune chunks.
        const string filter = """
            FROM ieq.measurements
            WHERE sensor_id = ANY(@ids) AND parameter_code = @pc AND is_invalid = false
              AND received_at >= @from AND received_at <= @to
            """;

        const string bucketsSql = $"""
            SELECT time_bucket(@interval::interval, received_at) AS t,
                   count(*),
                   min(value), max(value), avg(value),
                   percentile_cont(0.25) WITHIN GROUP (ORDER BY value),
                   percentile_cont(0.50) WITHIN GROUP (ORDER BY value),
                   percentile_cont(0.75) WITHIN GROUP (ORDER BY value)
            {filter}
            GROUP BY t
            ORDER BY t
            """;

        const string statsSql = $"""
            SELECT count(*),
                   min(value), max(value), avg(value),
                   percentile_cont(0.05) WITHIN GROUP (ORDER BY value),
                   percentile_cont(0.25) WITHIN GROUP (ORDER BY value),
                   percentile_cont(0.50) WITHIN GROUP (ORDER BY value),
                   percentile_cont(0.75) WITHIN GROUP (ORDER BY value),
                   percentile_cont(0.95) WITHIN GROUP (ORDER BY value),
                   min(unit)
            {filter}
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var buckets = new List<AggregateBucketDto>();
        await using (var command = new NpgsqlCommand(bucketsSql, connection))
        {
            AddIds(command, sensorIds);
            AddFilterScalars(command, parameterCode, from, to);
            command.Parameters.Add(new NpgsqlParameter("interval", NpgsqlDbType.Text) { Value = intervalLiteral });

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                buckets.Add(new AggregateBucketDto(
                    T: reader.GetDateTime(0),
                    Count: reader.GetInt64(1),
                    Min: reader.GetDouble(2),
                    Max: reader.GetDouble(3),
                    Avg: reader.GetDouble(4),
                    P25: reader.GetDouble(5),
                    P50: reader.GetDouble(6),
                    P75: reader.GetDouble(7)));
            }
        }

        AggregateStatsDto? stats = null;
        string? unit = null;
        await using (var command = new NpgsqlCommand(statsSql, connection))
        {
            AddIds(command, sensorIds);
            AddFilterScalars(command, parameterCode, from, to);

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct) && reader.GetInt64(0) > 0)
            {
                stats = new AggregateStatsDto(
                    Count: reader.GetInt64(0),
                    Min: reader.GetDouble(1),
                    Max: reader.GetDouble(2),
                    Avg: reader.GetDouble(3),
                    P05: reader.GetDouble(4),
                    P25: reader.GetDouble(5),
                    P50: reader.GetDouble(6),
                    P75: reader.GetDouble(7),
                    P95: reader.GetDouble(8));
                unit = reader.IsDBNull(9) ? null : reader.GetString(9);
            }
        }

        return new AggregateResult(buckets, stats, unit);
    }

    private static void AddIds(NpgsqlCommand command, IReadOnlyCollection<Guid> sensorIds) =>
        command.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            { Value = sensorIds.ToArray() });

    private static void AddFilterScalars(NpgsqlCommand command, string parameterCode, DateTime from, DateTime to)
    {
        command.Parameters.Add(new NpgsqlParameter("pc", NpgsqlDbType.Text) { Value = parameterCode });
        command.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.TimestampTz) { Value = from });
        command.Parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.TimestampTz) { Value = to });
    }
}
