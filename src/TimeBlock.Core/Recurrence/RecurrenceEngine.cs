namespace TimeBlock.Core.Recurrence;

public sealed record RecurrenceRule(
    string Frequency,
    int Interval,
    IReadOnlyList<DayOfWeek> ByDay,
    DateOnly? Until,
    int? Count);

public static class RecurrenceEngine
{
    private static readonly Dictionary<string, DayOfWeek> DayMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MO"] = DayOfWeek.Monday,
        ["TU"] = DayOfWeek.Tuesday,
        ["WE"] = DayOfWeek.Wednesday,
        ["TH"] = DayOfWeek.Thursday,
        ["FR"] = DayOfWeek.Friday,
        ["SA"] = DayOfWeek.Saturday,
        ["SU"] = DayOfWeek.Sunday
    };

    public static RecurrenceRule Parse(string rrule)
    {
        var frequency = "WEEKLY";
        var interval = 1;
        var byDay = new List<DayOfWeek>();
        DateOnly? until = null;
        int? count = null;

        foreach (var part in rrule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
            {
                continue;
            }

            var key = kv[0].Trim().ToUpperInvariant();
            var value = kv[1].Trim();

            switch (key)
            {
                case "FREQ":
                    frequency = value.ToUpperInvariant();
                    break;
                case "INTERVAL":
                    interval = int.TryParse(value, out var parsedInterval) && parsedInterval > 0 ? parsedInterval : 1;
                    break;
                case "BYDAY":
                    byDay = value
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(d => DayMap.TryGetValue(d, out var day) ? (DayOfWeek?)day : null)
                        .Where(d => d.HasValue)
                        .Select(d => d!.Value)
                        .Distinct()
                        .ToList();
                    break;
                case "UNTIL":
                    until = DateOnly.TryParseExact(value, "yyyyMMdd", out var parsedUntil) ? parsedUntil : null;
                    break;
                case "COUNT":
                    count = int.TryParse(value, out var parsedCount) && parsedCount > 0 ? parsedCount : null;
                    break;
            }
        }

        return new RecurrenceRule(frequency, interval, byDay, until, count);
    }

    public static IEnumerable<DateOnly> Expand(string rrule, DateOnly startDate, DateOnly windowStart, DateOnly windowEnd)
    {
        var rule = Parse(rrule);
        var end = rule.Until.HasValue && rule.Until.Value < windowEnd ? rule.Until.Value : windowEnd;
        var byDays = rule.ByDay.Count > 0 ? rule.ByDay : null;
        var produced = 0;

        foreach (var date in Occurrences(rule.Frequency, rule.Interval, byDays, startDate, end))
        {
            if (rule.Count.HasValue && produced >= rule.Count.Value)
            {
                yield break;
            }

            produced++;
            if (date >= windowStart && date <= windowEnd)
            {
                yield return date;
            }
        }
    }

    private static IEnumerable<DateOnly> Occurrences(
        string frequency,
        int interval,
        IReadOnlyList<DayOfWeek>? byDays,
        DateOnly start,
        DateOnly end)
    {
        if (frequency == "DAILY")
        {
            for (var date = start; date <= end; date = date.AddDays(interval))
            {
                if (byDays is null || byDays.Contains(date.DayOfWeek))
                {
                    yield return date;
                }
            }
        }
        else
        {
            var effectiveDays = byDays ?? new[] { start.DayOfWeek };
            var weekStart = StartOfWeek(start);
            for (var week = weekStart; week <= end; week = week.AddDays(7 * interval))
            {
                for (var date = week; date < week.AddDays(7); date = date.AddDays(1))
                {
                    if (date < start || date > end)
                    {
                        continue;
                    }

                    if (effectiveDays.Contains(date.DayOfWeek))
                    {
                        yield return date;
                    }
                }
            }
        }
    }

    public static DateOnly StartOfWeek(DateOnly date)
    {
        return date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    }
}
