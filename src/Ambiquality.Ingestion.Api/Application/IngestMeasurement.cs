namespace Ambiquality.Ingestion.Api.Application;

/// <summary>
/// A batch of readings reported by one sensor. The batch is validated and enqueued
/// atomically (all-or-nothing); see <see cref="IngestMeasurementHandler"/>.
/// </summary>
public sealed record IngestMeasurementsCommand(
    Guid SensorId,
    string PresentedApiKey,
    IReadOnlyList<MeasurementReadingInput> Readings);

/// <summary>A single quantity-value-unit triple within a batch.</summary>
public sealed record MeasurementReadingInput(string ParameterCode, double Value, string? Unit);

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

    /// <summary>
    /// The sensor published more frequently than its profile's reporting interval
    /// allows → 429; the result carries a <see cref="IngestMeasurementsResult.RetryAfterSeconds"/>.
    /// </summary>
    RateLimited,

    /// <summary>Sensor does not declare a reading's parameter (UC10 alt. A) → 422.</summary>
    ParameterNotDeclared,

    /// <summary>A reading's value lies outside the permitted range (UC10 alt. B) → 422.</summary>
    ValueOutOfRange,

    /// <summary>
    /// A reading omitted its unit or declared one that differs from the canonical unit
    /// configured for the parameter (UC10 alt. A — quantity and unit must match) → 422.
    /// </summary>
    UnitMismatch,

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

    /// <summary>
    /// For a <see cref="IngestRejectionReason.RateLimited"/> rejection, the seconds the
    /// sensor should wait before retrying (surfaced as the <c>Retry-After</c> header);
    /// <c>null</c> for every other outcome.
    /// </summary>
    public int? RetryAfterSeconds { get; private init; }

    public bool IsAccepted => Accepted is not null;

    public static IngestMeasurementsResult Accept(IReadOnlyList<AcceptedReading> accepted, DateTime receivedAt) =>
        new() { Accepted = accepted, ReceivedAt = receivedAt };

    public static IngestMeasurementsResult Reject(IngestRejectionReason reason, string detail) =>
        new() { Rejection = reason, Detail = detail };

    public static IngestMeasurementsResult RateLimited(string detail, int retryAfterSeconds) =>
        new() { Rejection = IngestRejectionReason.RateLimited, Detail = detail, RetryAfterSeconds = retryAfterSeconds };
}
