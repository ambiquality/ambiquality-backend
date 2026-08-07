using Ambiquality.Core.Messaging;
using Ambiquality.Ingestion.Worker.Monitoring;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Ambiquality.Ingestion.Worker;

/// <summary>
/// Drains the Redis ingestion stream into TimescaleDB. Reads new entries for this
/// consumer group in batches (blocking when idle), writes them via
/// <see cref="MeasurementBatchWriter"/>, then acknowledges only what was durably
/// written — so a crash between write and ack merely redelivers entries the
/// idempotent writer harmlessly skips. On startup and periodically it reclaims
/// entries left pending by a crashed consumer (<c>XAUTOCLAIM</c>).
/// </summary>
public sealed class MeasurementDrainService(
    IConnectionMultiplexer redis,
    MeasurementBatchWriter writer,
    IOptions<MeasurementQueueOptions> options,
    ILogger<MeasurementDrainService> logger,
    DrainStatus drainStatus) : BackgroundService
{
    private static readonly TimeSpan IdleReclaimAfter = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(5);

    private readonly MeasurementQueueOptions _options = options.Value;
    private readonly string _consumerName = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = redis.GetDatabase();
        await EnsureConsumerGroupAsync(db);

        // Recover anything a previous worker instance read but never acked.
        await ReclaimPendingAsync(db, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = await db.StreamReadGroupAsync(
                    _options.StreamKey, _options.ConsumerGroup, _consumerName,
                    StreamPosition.NewMessages, count: _options.BatchSize);

                if (entries.Length == 0)
                {
                    // No StackExchange.Redis blocking read; poll on the configured cadence.
                    await Task.Delay(_options.BlockMilliseconds, stoppingToken);
                    continue;
                }

                await ProcessAsync(db, entries, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Drain loop error; backing off {Backoff}.", ErrorBackoff);
                await Task.Delay(ErrorBackoff, stoppingToken);
            }
        }
    }

    private async Task EnsureConsumerGroupAsync(IDatabase db)
    {
        try
        {
            // position "0": a fresh group consumes the whole durable backlog.
            await db.StreamCreateConsumerGroupAsync(
                _options.StreamKey, _options.ConsumerGroup, StreamPosition.Beginning, createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists — expected on every start after the first.
        }
    }

    private async Task ReclaimPendingAsync(IDatabase db, CancellationToken ct)
    {
        var start = (RedisValue)"0-0";
        while (!ct.IsCancellationRequested)
        {
            var result = await db.StreamAutoClaimAsync(
                _options.StreamKey, _options.ConsumerGroup, _consumerName,
                (long)IdleReclaimAfter.TotalMilliseconds, start, count: _options.BatchSize);

            if (result.ClaimedEntries.Length > 0)
                await ProcessAsync(db, result.ClaimedEntries, ct);

            if (result.NextStartId == "0-0" || result.NextStartId.IsNullOrEmpty)
                break;
            start = result.NextStartId;
        }
    }

    private async Task ProcessAsync(IDatabase db, StreamEntry[] entries, CancellationToken ct)
    {
        var messages = new List<MeasurementMessage>(entries.Length);
        var ids = new List<RedisValue>(entries.Length);

        foreach (var entry in entries)
        {
            var payload = entry[_options.PayloadField];
            if (payload.IsNullOrEmpty)
            {
                // Malformed entry with no payload: ack it so it can't block the group.
                ids.Add(entry.Id);
                logger.LogWarning("Stream entry {EntryId} had no '{Field}' field; acking and skipping.",
                    entry.Id, _options.PayloadField);
                continue;
            }

            try
            {
                messages.Add(MeasurementMessageSerializer.Deserialize(payload!));
                ids.Add(entry.Id);
            }
            catch (Exception ex)
            {
                ids.Add(entry.Id);
                logger.LogWarning(ex, "Stream entry {EntryId} failed to deserialize; acking and skipping.", entry.Id);
            }
        }

        if (messages.Count > 0)
            await writer.WriteAsync(messages, ct);

        if (ids.Count > 0)
            await db.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, ids.ToArray());

        // Successfully drained a batch — publish the freshness timestamp for the
        // queue-lag gauge (only reached when the write above did not throw).
        drainStatus.MarkDrained();
    }
}
