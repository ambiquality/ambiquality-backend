namespace Ambiquality.Public.Api.Infrastructure.Catalog;

/// <summary>
/// Read-only access to the published monthly export objects, listed as downloadable
/// <c>dcat:Distribution</c> entries in the DCAT-AP catalog.
/// </summary>
public interface IExportCatalog
{
    Task<IReadOnlyList<ExportDistributionRow>> GetExportsAsync(CancellationToken ct);
}
