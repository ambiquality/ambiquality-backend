using System.Text.Json;
using Ambiquality.Observability;
using Microsoft.AspNetCore.OpenApi;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// Anonymous browser telemetry endpoint that feeds the Core Web Vitals histograms
/// (LCP / FID / INP / TTFB / CLS) into the <c>ambiquality.web_vitals.*</c> instruments.
///
/// The SPA reports with <c>navigator.sendBeacon</c> on pagehide using a <c>text/plain</c>
/// blob, which is a CORS-safelisted simple request against the (any-origin) Public.Api.
/// The endpoint is deliberately excluded from the published OpenAPI document: it is an
/// internal concern, not part of the open-data contract, and must not clutter the vendored
/// frontend API client. It is also completely anonymous — values are clamped, garbage is
/// dropped, and nothing about the caller is retained.
/// </summary>
public static class RumVitalsEndpoint
{
    public const string Route = "/telemetry/vitals";

    // Upper sanity bounds (ms). Anything beyond is a broken/clamped read and is dropped.
    private const double MaxTimingMilliseconds = 300_000; // 5 minutes
    private const double MaxCls = 1.0;

    private static readonly JsonSerializerOptions JsonOptions = new()
        { PropertyNameCaseInsensitive = true };

    public static void MapRumVitalsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost(Route, async (HttpRequest request, CancellationToken ct) =>
        {
            VitalsPayload? payload;
            try
            {
                payload = await JsonSerializer.DeserializeAsync<VitalsPayload>(
                    request.Body, JsonOptions, ct);
            }
            catch (JsonException)
            {
                // Garbage body — ignore it, never inform the reporter.
                return Results.NoContent();
            }

            if (payload is null)
                return Results.NoContent();

            var bucket = SanitizeRouteBucket(payload.RouteBucket);

            if (TryPositive(payload.Lcp, out var lcp))
                AmbiqualityMetrics.WebVitalsDuration.Record(
                    lcp, AmbiqualityMetrics.VitalsDurationTags("lcp", bucket));
            if (TryPositive(payload.Fid, out var fid))
                AmbiqualityMetrics.WebVitalsDuration.Record(
                    fid, AmbiqualityMetrics.VitalsDurationTags("fid", bucket));
            if (TryPositive(payload.Inp, out var inp))
                AmbiqualityMetrics.WebVitalsDuration.Record(
                    inp, AmbiqualityMetrics.VitalsDurationTags("inp", bucket));
            if (TryPositive(payload.Ttfb, out var ttfb))
                AmbiqualityMetrics.WebVitalsDuration.Record(
                    ttfb, AmbiqualityMetrics.VitalsDurationTags("ttfb", bucket));
            if (payload.Cls is { } cls && !double.IsNaN(cls) && cls >= 0 && cls <= MaxCls)
                AmbiqualityMetrics.WebVitalsCls.Record(cls, AmbiqualityMetrics.VitalsClsTags(bucket));

            AmbiqualityMetrics.WebVitalsPageviews.Add(1, AmbiqualityMetrics.PageviewTags(bucket));

            return Results.NoContent();
        })
        .WithName("ReportWebVitals")
        .WithSummary("Anonymized Core Web Vitals reporting — internal telemetry, not part of the open-data contract.")
        .WithMetadata(new ExcludeFromDescriptionAttribute());
    }

    /// <summary>Finite, non-negative, bounded above — anything else is dropped.</summary>
    private static bool TryPositive(double? value, out double result)
    {
        result = 0;
        if (value is not { } v || double.IsNaN(v) || double.IsInfinity(v) || v <= 0)
            return false;
        result = Math.Min(v, MaxTimingMilliseconds);
        return true;
    }

    /// <summary>Restrict to a small fixed set of route buckets to bound metric cardinality.</summary>
    private static string SanitizeRouteBucket(string? bucket) =>
        bucket is { Length: > 0 and <= 32 }
        && bucket.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')
            ? bucket
            : "other";
}

/// <summary>Anonymous web-vitals report body sent by the SPA's beacon on pagehide.</summary>
public sealed record VitalsPayload
{
    public string? RouteBucket { get; init; }
    public double? Lcp { get; init; }
    public double? Fid { get; init; }
    public double? Inp { get; init; }
    public double? Cls { get; init; }
    public double? Ttfb { get; init; }
}
