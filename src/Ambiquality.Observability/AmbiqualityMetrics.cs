using System.Diagnostics.Metrics;

namespace Ambiquality.Observability;

/// <summary>
/// The single <c>ambiquality</c> meter shared by every service. All metric names are
/// versioned here so the Prometheus scrape config and the Grafana dashboards (which
/// reference the emitted <c>ambiquality_*</c> series) stay in lockstep with the code.
/// Instruments created here are recorded by the services directly; the dashboards
/// expose them under the four Golden Signals labels.
/// </summary>
public static class AmbiqualityMetrics
{
    public const string MeterName = "ambiquality";

    public static readonly Meter Meter = new(MeterName);

    // ── Ingestion write path (Ingestion.Api) ───────────────────────────────────
    // Units are intentionally omitted so the Prometheus exporter doesn't append awkward
    // suffixes (measurements/archives/pages); only the duration histograms keep "ms".
    /// <summary>Batches accepted into the durable Redis queue, by outcome.</summary>
    public static readonly Counter<long> IngestionBatches = Meter.CreateCounter<long>(
        "ambiquality.ingestion.batches", unit: null,
        "Ingestion batches durably enqueued, tagged by outcome (accepted, validation_rejected, rate_limited, unauthorized, forbidden, enqueue_failed).");

    /// <summary>Individual readings durably enqueued (per accepted batch).</summary>
    public static readonly Counter<long> MeasurementEnqueued = Meter.CreateCounter<long>(
        "ambiquality.ingestion.measurements_enqueued", unit: null,
        "Readings durably enqueued to the Redis ingestion stream.");

    // ── Operator activity (Evidence.Api) ───────────────────────────────────────
    /// <summary>Distinct authenticated operators per rolling window (5m/1h/24h).</summary>
    public static readonly Gauge<long> ActiveUsers = Meter.CreateGauge<long>(
        "ambiquality.active_users", unit: null,
        "Distinct operators with activity in a rolling window.");

    // ── Queue saturation (Ingestion.Worker) ────────────────────────────────────
    /// <summary>Stream length — measurements enqueued but not yet drained.</summary>
    public static readonly Gauge<long> QueueMeasurementsPending = Meter.CreateGauge<long>(
        "ambiquality.queue.measurements_pending", unit: null,
        "Measurements in the Redis stream not yet drained by the worker.");

    /// <summary>Entries read by a consumer but not yet acknowledged (backlog).</summary>
    public static readonly Gauge<long> QueueMeasurementsUnacked = Meter.CreateGauge<long>(
        "ambiquality.queue.measurements_unacked", unit: null,
        "Redis stream entries read but not yet acknowledged.");

    /// <summary>Seconds since the worker last drained a batch (stale → stalled worker).</summary>
    public static readonly Gauge<long> QueueLastDrainGapSeconds = Meter.CreateGauge<long>(
        "ambiquality.queue.last_drain_gap_seconds", "seconds",
        "Seconds since the ingestion worker last drained the stream.");

    // ── Core Web Vitals (Public.Api RUM endpoint) ──────────────────────────────
    /// <summary>Browser-reported timings for lcp / inp / ttfb (ms).</summary>
    public static readonly Histogram<double> WebVitalsDuration = Meter.CreateHistogram<double>(
        "ambiquality.web_vitals.duration", "ms",
        "Browser-reported web vital timings (lcp, inp, ttfb), tagged by metric and route bucket.");

    /// <summary>Cumulative Layout Shift score (unitless).</summary>
    public static readonly Histogram<double> WebVitalsCls = Meter.CreateHistogram<double>(
        "ambiquality.web_vitals.cls", "1",
        "Browser-reported Cumulative Layout Shift scores.");

    /// <summary>Page loads that reported web vitals, tagged by route bucket.</summary>
    public static readonly Counter<long> WebVitalsPageviews = Meter.CreateCounter<long>(
        "ambiquality.web_vitals.pageviews", unit: null,
        "Page loads reporting web vitals, tagged by route bucket.");

    // ── Export worker throughput ───────────────────────────────────────────────
    /// <summary>Monthly measurement archives published to object storage.</summary>
    public static readonly Counter<long> ExportArchivesPublished = Meter.CreateCounter<long>(
        "ambiquality.export.archives_published", unit: null,
        "Monthly measurement archives published by the export worker.");

    // ── Attribute names ────────────────────────────────────────────────────────
    public const string WindowTag = "window";
    public const string OutcomeTag = "outcome";
    public const string MetricTag = "metric";
    public const string RouteBucketTag = "route_bucket";
    public const string StreamTag = "stream";
    public const string ConsumerGroupTag = "consumer_group";

    // ── Attribute values ───────────────────────────────────────────────────────
    public const string OutcomeAccepted = "accepted";
    public const string OutcomeValidationRejected = "validation_rejected";
    public const string OutcomeRateLimited = "rate_limited";
    public const string OutcomeUnauthorized = "unauthorized";
    public const string OutcomeForbidden = "forbidden";
    public const string OutcomeEnqueueFailed = "enqueue_failed";

    // ── Tag-set builders (kept next to the instruments so callers stay consistent) ──
    public static KeyValuePair<string, object?>[] ActiveUsersTags(string window) =>
        [new(WindowTag, window)];

    public static KeyValuePair<string, object?>[] IngestionBatchTags(string outcome) =>
        [new(OutcomeTag, outcome)];

    public static KeyValuePair<string, object?>[] QueuePendingTags(string stream) =>
        [new(StreamTag, stream)];

    public static KeyValuePair<string, object?>[] QueueUnackedTags(string stream, string group) =>
        [new(StreamTag, stream), new(ConsumerGroupTag, group)];

    public static KeyValuePair<string, object?>[] VitalsDurationTags(string metric, string bucket) =>
        [new(MetricTag, metric), new(RouteBucketTag, bucket)];

    public static KeyValuePair<string, object?>[] VitalsClsTags(string bucket) =>
        [new(RouteBucketTag, bucket)];

    public static KeyValuePair<string, object?>[] PageviewTags(string bucket) =>
        [new(RouteBucketTag, bucket)];
}
