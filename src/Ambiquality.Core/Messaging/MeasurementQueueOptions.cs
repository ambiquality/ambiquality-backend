namespace Ambiquality.Core.Messaging;

/// <summary>
/// Configuration shared by the ingestion queue producer (Ingestion.Api) and
/// consumer (Ingestion.Worker) so the stream key and consumer group can never
/// drift between the two services. Bound from the <c>MeasurementQueue</c>
/// configuration section.
/// </summary>
public sealed class MeasurementQueueOptions
{
    public const string SectionName = "MeasurementQueue";

    /// <summary>Redis stream the measurements are appended to.</summary>
    public string StreamKey { get; set; } = "ieq:measurements";

    /// <summary>Single field name carrying the serialized message in each stream entry.</summary>
    public string PayloadField { get; set; } = "payload";

    /// <summary>Consumer group the worker(s) read under (at-least-once with acks).</summary>
    public string ConsumerGroup { get; set; } = "writers";

    /// <summary>Max entries the worker pulls per read.</summary>
    public int BatchSize { get; set; } = 200;

    /// <summary>How long a worker read blocks waiting for new entries.</summary>
    public int BlockMilliseconds { get; set; } = 2000;

    /// <summary>
    /// Approximate cap on stream length (trims oldest acked-and-drained entries).
    /// Null disables trimming. Backpressure beyond this is bounded by Redis memory.
    /// </summary>
    public int? ApproxMaxLength { get; set; }
}
