using System.Runtime.CompilerServices;
using Ambiquality.Core.Domain.Measurements;
using Npgsql;
using NpgsqlTypes;

namespace Ambiquality.Export.Worker.Persistence;

/// <summary>
/// Raw Npgsql access to the <c>ieq</c> database for the export worker (same pattern as
/// Ingestion.Worker). Measurements are streamed one row at a time so a whole month
/// never lands in memory; exports are partitioned on the API-stamped
/// <c>received_at</c>, the hypertable's time column, so a month's slice prunes chunks.
/// </summary>
public sealed class ExportRepository(NpgsqlDataSource dataSource)
{
    /// <summary>Streams every measurement whose <c>received_at</c> falls in the half-open month window.</summary>
    public async IAsyncEnumerable<MeasurementRow> StreamMonthAsync(
        DateTime monthStartUtc, DateTime nextMonthStartUtc, [EnumeratorCancellation] CancellationToken ct)
    {
        const string sql = """
            SELECT id, sensor_id, parameter_code, value, unit, observed_at, received_at, is_invalid
            FROM ieq.measurements
            WHERE received_at >= @from AND received_at < @to
            ORDER BY received_at, id
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.TimestampTz) { Value = monthStartUtc });
        command.Parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.TimestampTz) { Value = nextMonthStartUtc });

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            yield return new MeasurementRow(
                Id: reader.GetGuid(0),
                SensorId: reader.GetGuid(1),
                ParameterCode: reader.GetString(2),
                Value: reader.GetDouble(3),
                Unit: reader.IsDBNull(4) ? null : reader.GetString(4),
                ObservedAt: reader.GetDateTime(5),
                ReceivedAt: reader.GetDateTime(6),
                IsInvalid: reader.GetBoolean(7));
        }
    }

    /// <summary>Media types already exported for the given month (used to skip completed work).</summary>
    public async Task<IReadOnlySet<string>> GetExportedMediaTypesAsync(short year, short month, CancellationToken ct)
    {
        const string sql = """
            SELECT media_type FROM ieq.measurement_exports
            WHERE year = @year AND month = @month
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("year", NpgsqlDbType.Smallint) { Value = year });
        command.Parameters.Add(new NpgsqlParameter("month", NpgsqlDbType.Smallint) { Value = month });

        var mediaTypes = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            mediaTypes.Add(reader.GetString(0));
        return mediaTypes;
    }

    /// <summary>Records one completed export. Idempotent against the unique (year, month, media type) index.</summary>
    public async Task InsertExportAsync(MeasurementExport export, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO ieq.measurement_exports
                (id, year, month, media_type, compress_format, file_key, download_url,
                 file_size_bytes, record_count, exported_at)
            VALUES
                (@id, @year, @month, @mediaType, @compressFormat, @fileKey, @downloadUrl,
                 @fileSizeBytes, @recordCount, @exportedAt)
            ON CONFLICT (year, month, media_type) DO NOTHING
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = export.Id });
        command.Parameters.Add(new NpgsqlParameter("year", NpgsqlDbType.Smallint) { Value = export.Year });
        command.Parameters.Add(new NpgsqlParameter("month", NpgsqlDbType.Smallint) { Value = export.Month });
        command.Parameters.Add(new NpgsqlParameter("mediaType", NpgsqlDbType.Varchar) { Value = export.MediaType });
        command.Parameters.Add(new NpgsqlParameter("compressFormat", NpgsqlDbType.Varchar) { Value = export.CompressFormat });
        command.Parameters.Add(new NpgsqlParameter("fileKey", NpgsqlDbType.Varchar) { Value = export.FileKey });
        command.Parameters.Add(new NpgsqlParameter("downloadUrl", NpgsqlDbType.Varchar) { Value = export.DownloadUrl });
        command.Parameters.Add(new NpgsqlParameter("fileSizeBytes", NpgsqlDbType.Bigint)
            { Value = (object?)export.FileSizeBytes ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("recordCount", NpgsqlDbType.Bigint)
            { Value = (object?)export.RecordCount ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("exportedAt", NpgsqlDbType.TimestampTz) { Value = export.ExportedAt });

        await command.ExecuteNonQueryAsync(ct);
    }
}
