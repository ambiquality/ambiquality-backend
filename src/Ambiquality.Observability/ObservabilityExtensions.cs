using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace Ambiquality.Observability;

/// <summary>
/// One shared OpenTelemetry metrics setup so every service exports the same RED,
/// runtime and business signals through the OTel Prometheus exporter. Every service —
/// HTTP API or background worker — serves <c>/metrics</c> on its own dedicated
/// <c>Observability:MetricsPort</c> via an <c>HttpListener</c>, so the scrape endpoint
/// can never be reached through the public Caddy routes and always looks identical to
/// Prometheus. Enable/disable via <c>Observability:Enabled</c> (turned off in
/// integration tests so no listener binds a fixed port there).
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>Config key for the internal /metrics port (set per service in compose).</summary>
    public const string MetricsPortConfigKey = "Observability:MetricsPort";

    /// <summary>Metrics are on when the key is absent or anything but "false".</summary>
    public static bool IsEnabled(IConfiguration config) =>
        config["Observability:Enabled"] is not "false";

    /// <summary>Reads <see cref="MetricsPortConfigKey"/>, falling back when unset/invalid.</summary>
    public static int ResolveMetricsPort(IConfiguration config, int fallback) =>
        int.TryParse(config[MetricsPortConfigKey], out var port) && port > 0 ? port : fallback;

    /// <summary>Registers HTTP client + runtime instruments, the <c>ambiquality</c> meter
    /// and the Prometheus HttpListener exporter on <paramref name="metricsPort"/>. The
    /// caller can add further instruments via <paramref name="instruments"/> — the API
    /// projects pass <c>m => m.AddAspNetCoreInstrumentation()</c> here so the ASP.NET
    /// Core instrumentation package (and its shared-framework dependency) stays out of
    /// the background workers.</summary>
    public static IServiceCollection AddAmbiqualityMetrics(
        this IServiceCollection services, int metricsPort,
        Action<OpenTelemetry.Metrics.MeterProviderBuilder>? instruments = null)
    {
        services.AddOpenTelemetry().WithMetrics(metrics =>
        {
            instruments?.Invoke(metrics);
            metrics
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(AmbiqualityMetrics.MeterName)
                .AddPrometheusHttpListener(options =>
                    ConfigureHttpListener(options, metricsPort));
        });
        return services;
    }

    /// <summary>Same as <see cref="AddAmbiqualityMetrics"/> but without the ASP.NET Core
    /// HTTP-server instrumentation (no web host). Used by the Ingestion and Export
    /// background workers, which have no request pipeline to measure.</summary>
    public static IServiceCollection AddAmbiqualityWorkerMetrics(
        this IServiceCollection services, int metricsPort)
    {
        services.AddOpenTelemetry().WithMetrics(metrics => metrics
            .AddRuntimeInstrumentation()
            .AddMeter(AmbiqualityMetrics.MeterName)
            .AddPrometheusHttpListener(options =>
                ConfigureHttpListener(options, metricsPort)));
        return services;
    }

    /// <summary>
    /// The exporter builds its prefix from <c>Host</c> via <see cref="UriBuilder"/>, which
    /// rejects the <c>*</c>/<c>+</c> wildcards that <see cref="HttpListener"/> needs to bind
    /// all interfaces (Prometheus scrapes from other containers over the compose network),
    /// while an explicit IP prefix fails <c>HttpListener.Start()</c> on Linux. The way out:
    /// keep a Uri-valid default host and replace the listener's prefixes with the wildcard
    /// here — <c>Start()</c> then binds <c>0.0.0.0</c>.
    /// </summary>
    private static void ConfigureHttpListener(
        OpenTelemetry.Exporter.PrometheusHttpListenerOptions options, int metricsPort)
    {
        options.Port = metricsPort;
        options.ConfigureHttpListener = (_, listener) =>
        {
            listener.Prefixes.Clear();
            listener.Prefixes.Add($"http://*:{metricsPort}/");
        };
        // Serve the classic Prometheus text format with underscore names (like
        // node-exporter) instead of OpenMetrics UTF-8 (dotted) names, so metric names such
        // as http_server_request_duration_seconds_count stay queryable with ordinary PromQL
        // and Grafana variables (no __name__ matchers needed).
        options.TranslationStrategy =
            OpenTelemetry.Exporter.PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes;
    }
}
