namespace Ambiquality.Core.Messaging;

/// <summary>
/// A validated observation handed off to the durable ingestion queue. This is the
/// wire contract shared between Ingestion.Api (producer) and Ingestion.Worker
/// (consumer); the worker materializes it into the <c>ieq.measurements</c>
/// hypertable. <see cref="ReceivedAt"/> is stamped by the API at acceptance time
/// and must survive the queue unchanged, so queue lag never shifts ingestion time.
/// <see cref="Id"/> is the measurement identity used as the worker's idempotency
/// key (at-least-once delivery, exactly-once effect).
/// </summary>
public sealed record MeasurementMessage(
    Guid Id,
    Guid SensorId,
    string ParameterCode,
    double Value,
    string? Unit,
    DateTime ObservedAt,
    DateTime ReceivedAt);
