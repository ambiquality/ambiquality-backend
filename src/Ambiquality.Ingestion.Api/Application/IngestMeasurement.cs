namespace Ambiquality.Ingestion.Api.Application;

public sealed record IngestMeasurementCommand(
    Guid SensorId,
    string PresentedApiKey,
    string ParameterCode,
    double Value);

/// <summary>Why an observation was rejected; maps to an HTTP status at the edge.</summary>
public enum IngestRejectionReason
{
    /// <summary>Unknown sensor or wrong API key (UC10 alt. C) → 401.</summary>
    Unauthorized,

    /// <summary>Sensor is registered but not in the active state (UC10 alt. C) → 403.</summary>
    SensorNotActive,

    /// <summary>Sensor does not declare the observation's parameter (UC10 alt. A) → 422.</summary>
    ParameterNotDeclared,

    /// <summary>Value lies outside the permitted range (UC10 alt. B) → 422.</summary>
    ValueOutOfRange,

    /// <summary>The durable queue could not accept the measurement → 503; nothing is acked.</summary>
    QueueUnavailable,
}

/// <summary>Outcome of an ingestion attempt: either accepted (persisted) or rejected.</summary>
public sealed record IngestMeasurementResult
{
    public Guid? MeasurementId { get; private init; }
    public DateTime? ReceivedAt { get; private init; }
    public IngestRejectionReason? Rejection { get; private init; }
    public string? Detail { get; private init; }

    public bool IsAccepted => MeasurementId is not null;

    public static IngestMeasurementResult Accepted(Guid id, DateTime receivedAt) =>
        new() { MeasurementId = id, ReceivedAt = receivedAt };

    public static IngestMeasurementResult Reject(IngestRejectionReason reason, string detail) =>
        new() { Rejection = reason, Detail = detail };
}
