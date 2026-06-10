using Ambiquality.Core.Messaging;

namespace Ambiquality.Ingestion.Api.Application.Abstractions;

/// <summary>
/// Durably appends a validated batch of measurements to the ingestion queue. The
/// publish must be durable <em>and atomic</em> before it returns (the queue is the
/// write-ahead log the HTTP 202 depends on — Durability NFR reinterpreted as
/// "durably enqueued before ack"); either every message in the batch lands or none
/// does, so a partial failure never leaves half a batch persisted. Throws if the
/// queue is unreachable so the caller can answer 503 rather than acknowledge a batch
/// it could not persist.
/// </summary>
public interface IMeasurementQueuePublisher
{
    Task PublishAsync(IReadOnlyList<MeasurementMessage> messages, CancellationToken cancellationToken);
}
