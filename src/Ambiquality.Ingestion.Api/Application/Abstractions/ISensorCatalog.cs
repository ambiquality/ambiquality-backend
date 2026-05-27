namespace Ambiquality.Ingestion.Api.Application.Abstractions;

/// <summary>
/// Read-only view of the Evidence sensor catalog used to validate incoming
/// measurements (UC10 steps 2-3). Backed by a direct read of the evidence
/// database (no HTTP) to keep the ingestion hot path cheap.
/// </summary>
public interface ISensorCatalog
{
    /// <summary>
    /// Returns the validation view for a sensor by id, or <c>null</c> if no such
    /// sensor exists. The status and declared parameters reflect the currently
    /// open (now-valid) temporal rows.
    /// </summary>
    Task<SensorValidationView?> FindSensorAsync(Guid sensorId, CancellationToken cancellationToken);
}

/// <param name="ApiKeyHash">SHA-256 hex of the sensor's API key.</param>
/// <param name="StatusCode">Currently open lifecycle status (e.g. <c>active</c>).</param>
/// <param name="DeclaredParameterCodes">Parameter codes the sensor currently declares.</param>
public sealed record SensorValidationView(
    string ApiKeyHash,
    string StatusCode,
    IReadOnlyCollection<string> DeclaredParameterCodes);
