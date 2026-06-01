using Ambiquality.Export.Worker.Persistence;

namespace Ambiquality.Export.Worker.Exporting;

/// <summary>
/// Periodically exports the most recent fully-elapsed calendar month to object
/// storage in every configured format. On each pass it skips formats already
/// recorded in <c>ieq.measurement_exports</c>, exports the rest, then sleeps until
/// 02:00 UTC on the first of the next month. A failed export backs off an hour and
/// retries rather than skipping the month.
/// </summary>
public sealed class MonthlyExportService(
    ExportRepository repository,
    MonthlyExporter exporter,
    ILogger<MonthlyExportService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromHours(1);

    private readonly TimeProvider _time = timeProvider;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = _time.GetUtcNow().UtcDateTime;
                var month = ExportMonth.MostRecentElapsed(now);
                await ExportMissingFormatsAsync(month, stoppingToken);

                var delay = NextWakeUtc(now) - now;
                await Task.Delay(delay, _time, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Export pass failed; retrying in {Backoff}.", ErrorBackoff);
                try
                {
                    await Task.Delay(ErrorBackoff, _time, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task ExportMissingFormatsAsync(ExportMonth month, CancellationToken ct)
    {
        var done = await repository.GetExportedMediaTypesAsync(month.Year, month.Month, ct);
        var missing = exporter.Formats.Where(f => !done.Contains(f.MediaType)).ToList();

        if (missing.Count == 0)
        {
            logger.LogInformation("Month {Year}-{Month:D2} already fully exported; nothing to do.",
                month.Year, month.Month);
            return;
        }

        foreach (var format in missing)
            await exporter.ExportAsync(month, format, ct);
    }

    /// <summary>02:00 UTC on the first day of the month after <paramref name="nowUtc"/>.</summary>
    internal static DateTime NextWakeUtc(DateTime nowUtc)
    {
        var firstOfThisMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return firstOfThisMonth.AddMonths(1).AddHours(2);
    }
}
