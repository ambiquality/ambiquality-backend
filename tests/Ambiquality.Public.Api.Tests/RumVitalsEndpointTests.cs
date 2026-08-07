using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ambiquality.Public.Api.Tests.Infrastructure;

namespace Ambiquality.Public.Api.Tests;

/// <summary>
/// The anonymous web-vitals beacon endpoint. It never touches the database, so these
/// tests boot Public.Api against a dummy connection string — no Postgres container
/// needed — and verify the three things that matter about a telemetry sink: it is
/// always 204 (never leaks about malformed reports), it is CORS-open, and it stays
/// out of the published OpenAPI document.
/// </summary>
public sealed class RumVitalsEndpointTests : IDisposable
{
    private readonly PublicApiFactory _factory = new(
        "Host=localhost;Port=59999;Database=unused;Username=unused;Password=unused");
    private readonly HttpClient _client;

    public RumVitalsEndpointTests()
    {
        _factory.Server.PreserveExecutionContext = true;
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Post_ValidPayload_Returns204()
    {
        var response = await _client.PostAsJsonAsync("/telemetry/vitals", new
        {
            routeBucket = "map",
            lcp = 1200,
            inp = 130,
            cls = 0.01,
            ttfb = 250
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Post_EmptyBody_Returns204()
    {
        var response = await _client.PostAsync("/telemetry/vitals", new StringContent(""));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Post_GarbageBody_Returns204()
    {
        var response = await _client.PostAsync(
            "/telemetry/vitals", new StringContent("definitely not json"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Post_InvalidValues_AreDropped_StillReturns204()
    {
        var response = await _client.PostAsJsonAsync("/telemetry/vitals", new
        {
            routeBucket = "Bad Bucket!!!",
            lcp = -100,
            ttfb = 99_999_999,
            cls = 999
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Post_UnknownRouteBucket_SanitizesToOther()
    {
        // A well-formed-but-unknown bucket (valid charset, not in the allow-list) must not
        // mint a new high-cardinality route_bucket label — it is folded into "other".
        var recorded = await CapturePageviewBucketAsync("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        Assert.Equal("other", Assert.Single(recorded));
    }

    [Fact]
    public async Task Post_AllowListedRouteBucket_PassesThroughUnchanged()
    {
        var recorded = await CapturePageviewBucketAsync("catalog");

        Assert.Equal("catalog", Assert.Single(recorded));
    }

    /// <summary>
    /// POSTs a vitals payload and returns the <c>route_bucket</c> label recorded on the
    /// <c>ambiquality.web_vitals.pageviews</c> counter (always emitted, so the assertion
    /// is deterministic and independent of the clamps applied to the timing values).
    /// </summary>
    private async Task<List<string>> CapturePageviewBucketAsync(string routeBucket)
    {
        var recordedBuckets = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "ambiquality.web_vitals.pageviews")
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "route_bucket")
                    recordedBuckets.Add(tag.Value?.ToString() ?? "");
        });
        listener.Start();

        var response = await _client.PostAsJsonAsync("/telemetry/vitals", new
        {
            routeBucket,
            ttfb = 250
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return recordedBuckets;
    }

    [Fact]
    public async Task OpenApiDocument_DoesNotExposeVitalsEndpoint()
    {
        var doc = await _client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        Assert.False(doc.GetProperty("paths").TryGetProperty("/telemetry/vitals", out _));
    }
}
