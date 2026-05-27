using Ambiquality.Ingestion.Api.Application.Abstractions;
using Npgsql;

namespace Ambiquality.Ingestion.Api.Infrastructure.Catalog;

/// <summary>
/// Reads the Evidence sensor catalog over a dedicated read-only connection
/// (the <c>ingestion_api</c> role has SELECT on the evidence schema). Queries are
/// schema-qualified (<c>evidence.*</c>) so they resolve whether the catalog lives
/// in a separate database (production) or a separate schema of the same database
/// (tests). <c>upper_inf(validity)</c> selects the currently open temporal rows.
/// </summary>
public sealed class SensorCatalog(NpgsqlDataSource dataSource) : ISensorCatalog
{
    public async Task<SensorValidationView?> FindSensorAsync(Guid sensorId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        string apiKeyHash;
        string statusCode;
        await using (var command = new NpgsqlCommand(
            // sensors."Id" is quoted PascalCase: the Evidence mapping leaves the
            // primary key column at its default name while other columns are snake_case.
            """
            SELECT s.api_key_hash, ss.status_code
            FROM evidence.sensors s
            JOIN evidence.sensor_status_history ss
              ON ss.sensor_id = s."Id" AND upper_inf(ss.validity)
            WHERE s."Id" = @id
            """, connection))
        {
            command.Parameters.AddWithValue("id", sensorId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            apiKeyHash = reader.GetString(0);
            statusCode = reader.GetString(1);
        }

        var parameterCodes = new List<string>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT parameter_code
            FROM evidence.sensor_measured_parameter_history
            WHERE sensor_id = @id AND upper_inf(validity)
            """, connection))
        {
            command.Parameters.AddWithValue("id", sensorId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                parameterCodes.Add(reader.GetString(0));
        }

        return new SensorValidationView(apiKeyHash, statusCode, parameterCodes);
    }
}
