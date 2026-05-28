using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Api.Application.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Ambiquality.Ingestion.Api.Infrastructure.Queue;

/// <summary>
/// Appends measurements to a Redis stream (<c>XADD</c>). The stream, persisted by
/// Redis AOF, is the durable write-ahead log the HTTP 202 depends on. A failed
/// <c>XADD</c> propagates so the handler answers 503 rather than acking an
/// observation it could not durably enqueue.
/// </summary>
public sealed class RedisMeasurementQueuePublisher(
    IConnectionMultiplexer redis,
    IOptions<MeasurementQueueOptions> options) : IMeasurementQueuePublisher
{
    private readonly MeasurementQueueOptions _options = options.Value;

    public async Task PublishAsync(MeasurementMessage message, CancellationToken cancellationToken)
    {
        var payload = MeasurementMessageSerializer.Serialize(message);
        var db = redis.GetDatabase();

        await db.StreamAddAsync(
            _options.StreamKey,
            _options.PayloadField,
            payload,
            maxLength: _options.ApproxMaxLength,
            useApproximateMaxLength: _options.ApproxMaxLength is not null);
    }
}
