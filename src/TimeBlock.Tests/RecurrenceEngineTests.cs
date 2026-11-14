using TimeBlock.Core.Recurrence;

namespace TimeBlock.Tests;

public class RecurrenceEngineTests
{
    [Fact]
    public void WeeklyByDayExpandsOnlySelectedDays()
    {
        var start = new DateOnly(2026, 11, 2);
        var from = new DateOnly(2026, 11, 2);
        var to = new DateOnly(2026, 11, 8);

        var dates = RecurrenceEngine.Expand("FREQ=WEEKLY;BYDAY=MO,WE,FR", start, from, to).ToList();

        Assert.Equal(new[] { new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 6) }, dates);
    }

    [Fact]
    public void WeeklyWithoutByDayUsesStartWeekday()
    {
        var start = new DateOnly(2026, 11, 3);
        var dates = RecurrenceEngine.Expand("FREQ=WEEKLY", start, start, start.AddDays(13)).ToList();

        Assert.Equal(2, dates.Count);
        Assert.All(dates, d => Assert.Equal(DayOfWeek.Tuesday, d.DayOfWeek));
    }

    [Fact]
    public void BiweeklySkipsAlternateWeeks()
    {
        var start = new DateOnly(2026, 11, 2);
        var dates = RecurrenceEngine.Expand("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO", start, start, start.AddDays(28)).ToList();

        Assert.Equal(new[] { new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 30) }, dates);
    }

    [Fact]
    public void DailyWithInterval()
    {
        var start = new DateOnly(2026, 11, 1);
        var dates = RecurrenceEngine.Expand("FREQ=DAILY;INTERVAL=3", start, start, start.AddDays(9)).ToList();

        Assert.Equal(new[] { new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 10) }, dates);
    }

    [Fact]
    public void UntilStopsExpansion()
    {
        var start = new DateOnly(2026, 11, 2);
        var dates = RecurrenceEngine.Expand("FREQ=DAILY;UNTIL=20261104", start, start, start.AddDays(30)).ToList();

        Assert.Equal(new[] { new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 4) }, dates);
    }

    [Fact]
    public void CountLimitsOccurrencesFromStart()
    {
        var start = new DateOnly(2026, 11, 2);
        var dates = RecurrenceEngine.Expand("FREQ=DAILY;COUNT=3", start, start.AddDays(1), start.AddDays(10)).ToList();

        Assert.Equal(new[] { new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 4) }, dates);
    }

    [Fact]
    public void WindowOutsideStartYieldsNothing()
    {
        var start = new DateOnly(2027, 1, 1);
        var dates = RecurrenceEngine.Expand("FREQ=DAILY", start, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Empty(dates);
    }

    [Fact]
    public void StartOfWeekIsMonday()
    {
        Assert.Equal(new DateOnly(2026, 11, 2), RecurrenceEngine.StartOfWeek(new DateOnly(2026, 11, 2)));
        Assert.Equal(new DateOnly(2026, 11, 2), RecurrenceEngine.StartOfWeek(new DateOnly(2026, 11, 8)));
        Assert.Equal(new DateOnly(2026, 11, 2), RecurrenceEngine.StartOfWeek(new DateOnly(2026, 11, 6)));
    }
}
