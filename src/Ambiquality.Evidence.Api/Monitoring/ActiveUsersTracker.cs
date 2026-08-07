using Ambiquality.Observability;

namespace Ambiquality.Evidence.Api.Monitoring;

/// <summary>
/// Tracks distinct authenticated operators in rolling windows and publishes them as the
/// <c>ambiquality.active_users</c> gauge ({window} ∈ 5m/1h/24h) for the Global Overview
/// dashboard's "Total Active Users" panel. <see cref="RecordActivity"/> is called from
/// <see cref="Infrastructure.Security.CurrentUserMiddleware"/> for every authenticated
/// request; a periodic loop prunes stale entries and refreshes the gauge.
/// </summary>
public sealed class ActiveUsersTracker(ILogger<ActiveUsersTracker> logger) : BackgroundService
{
    private static readonly TimeSpan MaxWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    private static readonly (string Label, TimeSpan Window)[] Windows =
    [
        ("5m", TimeSpan.FromMinutes(5)),
        ("1h", TimeSpan.FromHours(1)),
        ("24h", TimeSpan.FromHours(24))
    ];

    private readonly RollingActivityGauge _activity = new(MaxWindow);

    /// <summary>Called per authenticated request; only the most recent activity per user is kept.</summary>
    public void RecordActivity(Guid userId, DateTime utcNow) =>
        _activity.Record(userId.ToString(), new DateTimeOffset(utcNow, TimeSpan.Zero).ToUnixTimeSeconds());

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _activity.Prune(unixNow);
                foreach (var (label, window) in Windows)
                    AmbiqualityMetrics.ActiveUsers.Record(
                        _activity.CountInWindow(window, unixNow),
                        AmbiqualityMetrics.ActiveUsersTags(label));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refresh active-user metrics.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }
    }
}
