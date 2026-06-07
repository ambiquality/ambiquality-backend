namespace Ambiquality.Public.Api.Api;

/// <summary>
/// One time bucket of aggregated observations. <see cref="Avg"/> drives the trend line
/// (with <see cref="Min"/>/<see cref="Max"/> shaded); the quartiles fill in the band.
/// </summary>
public sealed record AggregateBucketDto(
    DateTime T,
    long Count,
    double Min,
    double Max,
    double Avg,
    double P25,
    double P50,
    double P75);

/// <summary>
/// Distribution summary over the whole window, driving the boxplot. <c>null</c> when the
/// window holds no observations (the frontend renders an empty state).
/// </summary>
public sealed record AggregateStatsDto(
    long Count,
    double Min,
    double Max,
    double Avg,
    double P05,
    double P25,
    double P50,
    double P75,
    double P95);

/// <summary>
/// Server-side aggregation of a quantity over a window for a building (across its sensors)
/// or a single sensor. <see cref="Bucket"/> is the resolved granularity (the auto-selected
/// one when the request asked for <c>auto</c>).
/// </summary>
public sealed record AggregateResponse(
    string ParameterCode,
    string? Unit,
    DateTime From,
    DateTime To,
    string Bucket,
    IReadOnlyList<AggregateBucketDto> Buckets,
    AggregateStatsDto? Stats,
    string License);
