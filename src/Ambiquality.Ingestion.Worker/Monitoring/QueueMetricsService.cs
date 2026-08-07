using Ambiquality.Core.Messaging;
using Ambiquality.Observability;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Ambiquality.Ingestion.Worker.Monitoring;

/// <summary>
/// Publishes the ingestion queue saturation gauges (the "S" of USE for the write path):
/// stream length (enqueued but not yet drained), pending-unacked backlog and seconds
/// since the last drain. Prometheus scrapes the same shared <c>/metrics</c> listener the
/// worker hosts on its <c>Observability:MetricsPort</c>.
/// </summary>
public sealed class QueueMetricsService(
    IConnectionMultiplexer redis,
    IOptions<MeasurementQueueOptions> options,
    DrainStatus drainStatus,
    ILogger<QueueMetricsService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);

    private readonly MeasurementQueueOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Queue metrics refresh failed; will retry next tick.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }
    }

    private async Task RefreshAsync()
    {
        var db = redis.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var pending = (long)await db.StreamLengthAsync(_options.StreamKey);

        var unacked = 0L;
        try
        {
            var summary = await db.StreamPendingAsync(_options.StreamKey, _options.ConsumerGroup);
            unacked = summary.PendingMessageCount;
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "XPENDING for {Stream}/{Group} failed.",
                _options.StreamKey, _options.ConsumerGroup);
        }

        var lastDrain = drainStatus.LastDrainUnixSeconds;

        AmbiqualityMetrics.QueueMeasurementsPending.Record(
            pending, AmbiqualityMetrics.QueuePendingTags(_options.StreamKey));
        AmbiqualityMetrics.QueueMeasurementsUnacked.Record(
            unacked, AmbiqualityMetrics.QueueUnackedTags(_options.StreamKey, _options.ConsumerGroup));
        AmbiqualityMetrics.QueueLastDrainGapSeconds.Record(
            lastDrain == 0 ? 0 : Math.Max(0, now - lastDrain));
    }
}
