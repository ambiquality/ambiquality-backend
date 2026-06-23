namespace Ambiquality.Ingestion.Api.Application;

/// <summary>
/// Per-sensor ingestion rate-limit configuration, bound from the
/// <c>IngestionRateLimit</c> section. A sensor may publish at most
/// <see cref="PermitsPerWindow"/> batches per window, where the window is the sensor's
/// own declared reporting interval (F08 <c>measurement_frequency_seconds</c>), clamped
/// to <see cref="MinIntervalSeconds"/> and defaulting to <see cref="DefaultIntervalSeconds"/>
/// when the sensor has not declared one. The limit is keyed by sensor id (one API key
/// per sensor), so a busy sensor can never starve another.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "IngestionRateLimit";

    /// <summary>Master switch; when false the limiter is bypassed entirely.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Window used when a sensor declares no reporting interval (5 minutes).</summary>
    public int DefaultIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Floor on the window. Enforces the ">= 5 min granularity" rule even if a sensor
    /// declares a tighter frequency: the effective window is never shorter than this.
    /// </summary>
    public int MinIntervalSeconds { get; set; } = 300;

    /// <summary>How many batches a sensor may publish per window before being throttled.</summary>
    public int PermitsPerWindow { get; set; } = 1;

    /// <summary>Redis key prefix for the per-sensor counter.</summary>
    public string KeyPrefix { get; set; } = "ieq:ingest:rl:";

    /// <summary>
    /// The window applied to a sensor: its declared interval (or the default when none),
    /// never below <see cref="MinIntervalSeconds"/>.
    /// </summary>
    public int WindowFor(int? declaredIntervalSeconds) =>
        Math.Max(declaredIntervalSeconds ?? DefaultIntervalSeconds, MinIntervalSeconds);
}
