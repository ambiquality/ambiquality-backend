using Ambiquality.Public.Api.Api;

namespace Ambiquality.Public.Api.Infrastructure.Observations;

/// <summary>The most-recent valid observation a sensor reported for a given quantity.</summary>
public sealed record LatestObservation(
    Guid SensorId, double Value, string? Unit, DateTime ObservedAt, DateTime ReceivedAt);

/// <summary>The bucketed series plus the overall distribution for an aggregation window.</summary>
public sealed record AggregateResult(
    IReadOnlyList<AggregateBucketDto> Buckets, AggregateStatsDto? Stats, string? Unit);

/// <summary>
/// Read-only analytical queries over the <c>ieq.measurements</c> hypertable, run with
/// raw Npgsql over the <c>public_api</c> connection so TimescaleDB-specific functions
/// (<c>time_bucket</c>, <c>percentile_cont</c>, <c>DISTINCT ON</c>) are available — the
/// same raw-reader pattern as <see cref="Catalog.ExportCatalog"/>. Invalidated rows are
/// always excluded (the public API never surfaces soft-deleted measurements here).
/// </summary>
public interface IMeasurementReader
{
    /// <summary>
    /// The latest valid observation of <paramref name="parameterCode"/> for each of the
    /// given sensors, in one round trip. Sensors with no matching observation are absent.
    /// </summary>
    Task<IReadOnlyList<LatestObservation>> GetLatestPerSensorAsync(
        IReadOnlyCollection<Guid> sensorIds, string parameterCode, CancellationToken ct);

    /// <summary>
    /// Buckets and overall percentiles for <paramref name="parameterCode"/> over the given
    /// sensors within <c>[from, to]</c>, bucketed by the Postgres <paramref name="intervalLiteral"/>
    /// (e.g. <c>1 hour</c>). <see cref="AggregateResult.Stats"/> is <c>null</c> when the window is empty.
    /// </summary>
    Task<AggregateResult> AggregateAsync(
        IReadOnlyCollection<Guid> sensorIds, string parameterCode, DateTime from, DateTime to,
        string intervalLiteral, CancellationToken ct);
}
