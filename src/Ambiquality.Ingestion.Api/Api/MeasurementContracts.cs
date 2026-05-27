namespace Ambiquality.Ingestion.Api.Api;

/// <summary>
/// A single observation reported by a sensor (UC10 step 1). The sensor's secret
/// key travels in the <c>X-Sensor-Key</c> header, not the body.
/// </summary>
public sealed record IngestMeasurementRequest(
    Guid SensorId,
    string ParameterCode,
    double Value,
    DateTime ObservedAt);

public sealed record MeasurementAcceptedResponse(Guid Id, DateTime ReceivedAt);
