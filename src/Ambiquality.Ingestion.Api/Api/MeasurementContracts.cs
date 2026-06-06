namespace Ambiquality.Ingestion.Api.Api;

/// <summary>
/// A single observation reported by a sensor (UC10 step 1). The sensor's secret
/// key travels in the <c>X-Sensor-Key</c> header, not the body. The observation
/// timestamp is <em>not</em> accepted from the sensor — the API stamps it on
/// acceptance from the trusted server clock, so an unsynchronized sensor clock
/// can never skew the recorded time (see the ingestion handler).
/// </summary>
public sealed record IngestMeasurementRequest(
    Guid SensorId,
    string ParameterCode,
    double Value);

public sealed record MeasurementAcceptedResponse(Guid Id, DateTime ReceivedAt);
