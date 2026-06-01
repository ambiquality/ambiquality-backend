using Ambiquality.Export.Worker.Exporting;

namespace Ambiquality.Export.Worker.Tests;

/// <summary>Unit coverage for the month-selection and wake-time arithmetic (no I/O).</summary>
public sealed class ExportScheduleTests
{
    [Theory]
    [InlineData(2026, 6, 1, 2026, 5)]   // mid-year: previous month
    [InlineData(2026, 1, 15, 2025, 12)] // January rolls back to December of the prior year
    [InlineData(2026, 3, 1, 2026, 2)]   // first day still exports the fully-elapsed prior month
    public void MostRecentElapsed_IsThePreviousCalendarMonth(
        int y, int mo, int d, int expectedYear, int expectedMonth)
    {
        var now = new DateTime(y, mo, d, 9, 0, 0, DateTimeKind.Utc);

        var month = ExportMonth.MostRecentElapsed(now);

        Assert.Equal((short)expectedYear, month.Year);
        Assert.Equal((short)expectedMonth, month.Month);
    }

    [Fact]
    public void ExportMonth_WindowIsHalfOpenAcrossTheCalendarMonth()
    {
        var month = new ExportMonth(2026, 5);

        Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), month.StartUtc);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), month.NextMonthStartUtc);
    }

    [Theory]
    [InlineData(2026, 5, 15, 2026, 6, 1)]
    [InlineData(2026, 12, 31, 2027, 1, 1)] // December wakes on the first of next January
    public void NextWakeUtc_Is0200OnTheFirstOfNextMonth(
        int y, int mo, int d, int wy, int wmo, int wd)
    {
        var now = new DateTime(y, mo, d, 14, 30, 0, DateTimeKind.Utc);

        var wake = MonthlyExportService.NextWakeUtc(now);

        Assert.Equal(new DateTime(wy, wmo, wd, 2, 0, 0, DateTimeKind.Utc), wake);
    }
}
