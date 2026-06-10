using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Api.Application.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Ambiquality.Ingestion.Api.Infrastructure.Queue;

/// <summary>
/// Appends measurements to a Redis stream (<c>XADD</c>). The stream, persisted by
/// Redis AOF, is the durable write-ahead log the HTTP 202 depends on. A failed
/// <c>XADD</c> propagates so the handler answers 503 rather than acking a batch it
/// could not durably enqueue. A multi-reading batch is appended inside a
/// <c>MULTI</c>/<c>EXEC</c> transaction so it lands atomically — a partial failure
/// never leaves half a batch in the stream.
/// </summary>
public sealed class RedisMeasurementQueuePublisher(
    IConnectionMultiplexer redis,
    IOptions<MeasurementQueueOptions> options) : IMeasurementQueuePublisher
{
    private readonly MeasurementQueueOptions _options = options.Value;

    public async Task PublishAsync(IReadOnlyList<MeasurementMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
            return;

        var db = redis.GetDatabase();

        // Single reading: a plain XADD is already atomic, no transaction overhead.
        if (messages.Count == 1)
        {
            await AddAsync(db, messages[0]);
            return;
        }

        // Multiple readings: MULTI/EXEC so the whole batch lands or none of it does.
        var transaction = db.CreateTransaction();
        foreach (var message in messages)
            _ = AddAsync(transaction, message);

        if (!await transaction.ExecuteAsync())
            throw new RedisException("The measurement batch transaction was not committed.");
    }

    private Task AddAsync(IDatabaseAsync db, MeasurementMessage message) =>
        db.StreamAddAsync(
            _options.StreamKey,
            _options.PayloadField,
            MeasurementMessageSerializer.Serialize(message),
            maxLength: _options.ApproxMaxLength,
            useApproximateMaxLength: _options.ApproxMaxLength is not null);
}
