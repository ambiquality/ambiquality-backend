namespace Ambiquality.Ingestion.Api.Application;

/// <summary>
/// A batch of readings reported by one sensor. The batch is validated and enqueued
/// atomically (all-or-nothing); see <see cref="IngestMeasurementHandler"/>.
/// </summary>
public sealed record IngestMeasurementsCommand(
    Guid SensorId,
    string PresentedApiKey,
    IReadOnlyList<MeasurementReadingInput> Readings);

/// <summary>A single quantity-value pair within a batch.</summary>
public sealed record MeasurementReadingInput(string ParameterCode, double Value);

/// <summary>One reading that was accepted: the assigned measurement id and its parameter.</summary>
public sealed record AcceptedReading(Guid Id, string ParameterCode);

/// <summary>Why a batch was rejected; maps to an HTTP status at the edge.</summary>
public enum IngestRejectionReason
{
    /// <summary>The batch carried no readings (UC10 — nothing to validate) → 422.</summary>
    EmptyBatch,

    /// <summary>The same parameter appears more than once in the batch → 422.</summary>
    DuplicateParameter,

    /// <summary>Unknown sensor or wrong API key (UC10 alt. C) → 401.</summary>
    Unauthorized,

    /// <summary>Sensor is registered but not in the active state (UC10 alt. C) → 403.</summary>
    SensorNotActive,

    /// <summary>Sensor does not declare a reading's parameter (UC10 alt. A) → 422.</summary>
    ParameterNotDeclared,

    /// <summary>A reading's value lies outside the permitted range (UC10 alt. B) → 422.</summary>
    ValueOutOfRange,

    /// <summary>The durable queue could not accept the batch → 503; nothing is acked.</summary>
    QueueUnavailable,
}

/// <summary>Outcome of an ingestion attempt: either the whole batch is accepted or it is rejected.</summary>
public sealed record IngestMeasurementsResult
{
    public IReadOnlyList<AcceptedReading>? Accepted { get; private init; }
    public DateTime? ReceivedAt { get; private init; }
    public IngestRejectionReason? Rejection { get; private init; }
    public string? Detail { get; private init; }

    public bool IsAccepted => Accepted is not null;

    public static IngestMeasurementsResult Accept(IReadOnlyList<AcceptedReading> accepted, DateTime receivedAt) =>
        new() { Accepted = accepted, ReceivedAt = receivedAt };

    public static IngestMeasurementsResult Reject(IngestRejectionReason reason, string detail) =>
        new() { Rejection = reason, Detail = detail };
}
