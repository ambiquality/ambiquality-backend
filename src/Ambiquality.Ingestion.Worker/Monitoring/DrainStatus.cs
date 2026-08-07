namespace Ambiquality.Ingestion.Worker.Monitoring;

/// <summary>
/// Tracks when the drain loop last wrote a batch to the hypertable, so the
/// <c>ambiquality.queue.last_drain_gap_seconds</c> gauge can flag a stalled worker
/// (queue filling while nothing drains). Updated by <see cref="MeasurementDrainService"/>
/// after every successful batch write; read by <see cref="QueueMetricsService"/>.
/// </summary>
public sealed class DrainStatus
{
    private long _lastDrainUnixSeconds;

    public void MarkDrained() =>
        Interlocked.Exchange(ref _lastDrainUnixSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public long LastDrainUnixSeconds => Interlocked.Read(ref _lastDrainUnixSeconds);
}
