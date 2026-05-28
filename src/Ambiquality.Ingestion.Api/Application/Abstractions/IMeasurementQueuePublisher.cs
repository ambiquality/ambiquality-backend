using Ambiquality.Core.Messaging;

namespace Ambiquality.Ingestion.Api.Application.Abstractions;

/// <summary>
/// Durably appends a validated measurement to the ingestion queue. The publish
/// must be durable before it returns (the queue is the write-ahead log the HTTP
/// 202 depends on — Durability NFR reinterpreted as "durably enqueued before
/// ack"). Throws if the queue is unreachable so the caller can answer 503 rather
/// than acknowledge an observation it could not persist.
/// </summary>
public interface IMeasurementQueuePublisher
{
    Task PublishAsync(MeasurementMessage message, CancellationToken cancellationToken);
}
