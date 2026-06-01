namespace Ambiquality.Export.Worker.Exporting;

/// <summary>A fully-elapsed calendar month to export, with its UTC half-open window.</summary>
public readonly record struct ExportMonth(short Year, short Month)
{
    public DateTime StartUtc => new(Year, Month, 1, 0, 0, 0, DateTimeKind.Utc);

    public DateTime NextMonthStartUtc => StartUtc.AddMonths(1);

    /// <summary>The most recent fully-elapsed calendar month relative to <paramref name="nowUtc"/>.</summary>
    public static ExportMonth MostRecentElapsed(DateTime nowUtc)
    {
        var firstOfThisMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previous = firstOfThisMonth.AddMonths(-1);
        return new ExportMonth((short)previous.Year, (short)previous.Month);
    }
}
