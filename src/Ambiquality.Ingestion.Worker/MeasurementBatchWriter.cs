using System.Text;
using Ambiquality.Core.Messaging;
using Npgsql;
using NpgsqlTypes;

namespace Ambiquality.Ingestion.Worker;

/// <summary>
/// Bulk-inserts a batch of measurements into the <c>ieq.measurements</c> hypertable
/// in a single round trip. Inserts are idempotent on the measurement identity
/// (<c>(id, received_at)</c> — the hypertable's composite key): redelivered
/// messages from the at-least-once queue collide and are skipped, giving
/// exactly-once effect. Values are never updated, honoring measurement immutability.
/// </summary>
public sealed class MeasurementBatchWriter(NpgsqlDataSource dataSource)
{
    /// <summary>Inserts the batch, skipping rows already present. Returns rows newly inserted.</summary>
    public async Task<int> WriteAsync(IReadOnlyList<MeasurementMessage> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return 0;

        var sql = new StringBuilder(
            "INSERT INTO ieq.measurements " +
            "(id, sensor_id, parameter_code, value, unit, observed_at, received_at, is_invalid) VALUES ");

        await using var command = dataSource.CreateCommand();

        for (var i = 0; i < batch.Count; i++)
        {
            if (i > 0)
                sql.Append(',');
            sql.Append($"(@id{i}, @sensor{i}, @pc{i}, @val{i}, @unit{i}, @obs{i}, @rcv{i}, false)");

            var m = batch[i];
            command.Parameters.AddWithValue($"id{i}", NpgsqlDbType.Uuid, m.Id);
            command.Parameters.AddWithValue($"sensor{i}", NpgsqlDbType.Uuid, m.SensorId);
            command.Parameters.AddWithValue($"pc{i}", NpgsqlDbType.Varchar, m.ParameterCode);
            command.Parameters.AddWithValue($"val{i}", NpgsqlDbType.Double, m.Value);
            command.Parameters.AddWithValue($"unit{i}", NpgsqlDbType.Varchar, (object?)m.Unit ?? DBNull.Value);
            command.Parameters.AddWithValue($"obs{i}", NpgsqlDbType.TimestampTz, m.ObservedAt);
            command.Parameters.AddWithValue($"rcv{i}", NpgsqlDbType.TimestampTz, m.ReceivedAt);
        }

        // The hypertable's composite key is (id, received_at); a redelivered message
        // carries the same id and acceptance timestamp, so it collides and is skipped.
        sql.Append(" ON CONFLICT (id, received_at) DO NOTHING");
        command.CommandText = sql.ToString();

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
