using Npgsql;

namespace Ambiquality.Public.Api.Infrastructure.Catalog;

/// <summary>One published monthly export object, projected into a DCAT download distribution.</summary>
public sealed record ExportDistributionRow(
    short Year,
    short Month,
    string MediaType,
    string CompressFormat,
    string DownloadUrl,
    long? FileSizeBytes);

/// <summary>
/// Read-only access to the published monthly exports (<c>ieq.measurement_exports</c>)
/// over the <c>public_api</c> connection — same raw-Npgsql pattern as
/// <see cref="EvidenceCatalog"/>. Used by the DCAT catalog to list downloadable
/// archive distributions.
/// </summary>
public sealed class ExportCatalog(NpgsqlDataSource dataSource) : IExportCatalog
{
    public async Task<IReadOnlyList<ExportDistributionRow>> GetExportsAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT year, month, media_type, compress_format, download_url, file_size_bytes
            FROM ieq.measurement_exports
            ORDER BY year DESC, month DESC, media_type
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);

        var rows = new List<ExportDistributionRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new ExportDistributionRow(
                Year: reader.GetInt16(0),
                Month: reader.GetInt16(1),
                MediaType: reader.GetString(2),
                CompressFormat: reader.GetString(3),
                DownloadUrl: reader.GetString(4),
                FileSizeBytes: reader.IsDBNull(5) ? null : reader.GetInt64(5)));
        }
        return rows;
    }
}
