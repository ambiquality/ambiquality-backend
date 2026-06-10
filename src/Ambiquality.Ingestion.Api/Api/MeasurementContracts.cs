namespace Ambiquality.Ingestion.Api.Api;

/// <summary>
/// A batch of observations reported by one sensor in a single request (UC10 step 1).
/// A sensor that measures several quantities sends them together: one
/// <see cref="MeasurementReading"/> per quantity it actually measures — it is never
/// forced to report parameters it has no probe for. The sensor's secret key travels
/// in the <c>X-Sensor-Key</c> header, not the body.
/// <para>
/// The batch is <strong>all-or-nothing</strong>: if any reading fails validation the
/// whole request is rejected and nothing is enqueued (see the ingestion handler). The
/// observation timestamp is <em>not</em> accepted from the sensor — the API stamps it on
/// acceptance from the trusted server clock, so an unsynchronized sensor clock can never
/// skew the recorded time.
/// </para>
/// </summary>
public sealed record IngestMeasurementsRequest(
    Guid SensorId,
    IReadOnlyList<MeasurementReading> Readings);

/// <summary>A single quantity-value pair within a batch (e.g. <c>co2</c> = 812).</summary>
public sealed record MeasurementReading(
    string ParameterCode,
    double Value);

/// <summary>
/// Acknowledges a durably-enqueued batch. <see cref="ReceivedAt"/> is shared by every
/// reading in the batch (a single server clock read at acceptance); each accepted reading
/// carries the measurement id it was assigned, correlated by <c>parameterCode</c>.
/// </summary>
public sealed record MeasurementsAcceptedResponse(
    DateTime ReceivedAt,
    IReadOnlyList<AcceptedMeasurement> Measurements);

/// <summary>One accepted reading: the assigned measurement id and the parameter it carries.</summary>
public sealed record AcceptedMeasurement(Guid Id, string ParameterCode);
