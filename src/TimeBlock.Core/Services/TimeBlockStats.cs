using Microsoft.EntityFrameworkCore;
using TimeBlock.Core.Contracts;
using TimeBlock.Core.Data;
using TimeBlock.Core.Entities;
using TimeBlock.Core.Recurrence;

namespace TimeBlock.Core.Services;

public class TimeBlockStats(TimeBlockDbContext db)
{
    public async Task<List<WeekFollowThroughDto>> WeeklyFollowThroughAsync(Guid userId, int weeks, CancellationToken ct = default)
    {
        weeks = Math.Clamp(weeks, 1, 26);
        var currentWeekStart = RecurrenceEngine.StartOfWeek(DateOnly.FromDateTime(DateTime.UtcNow));
        var firstWeekStart = currentWeekStart.AddDays(-7 * (weeks - 1));
        var windowEnd = currentWeekStart.AddDays(7);
        var from = firstWeekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = windowEnd.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var blocks = await db.Blocks
            .Where(b => b.OwnerId == userId && b.EndUtc >= from && b.EndUtc < to && b.Status != BlockStatus.Planned)
            .Select(b => new { b.EndUtc, b.Status })
            .ToListAsync(ct);

        var byWeek = blocks
            .GroupBy(b => RecurrenceEngine.StartOfWeek(DateOnly.FromDateTime(b.EndUtc)))
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<WeekFollowThroughDto>();
        for (var week = firstWeekStart; week < windowEnd; week = week.AddDays(7))
        {
            byWeek.TryGetValue(week, out var list);
            list ??= new();
            var done = list.Count(b => b.Status == BlockStatus.Done);
            var partial = list.Count(b => b.Status == BlockStatus.PartiallyDone);
            var skipped = list.Count(b => b.Status == BlockStatus.Skipped);
            var missed = list.Count(b => b.Status == BlockStatus.Missed);
            result.Add(new WeekFollowThroughDto(week, done, partial, skipped, missed, FollowThroughRate(done, partial, skipped, missed)));
        }

        return result;
    }

    public static double FollowThroughRate(int done, int partial, int skipped, int missed)
    {
        var total = done + partial + skipped + missed;
        return total == 0 ? 1.0 : (done + 0.5 * partial) / total;
    }

    public async Task<List<DayLoad>> DailyLoadAsync(Guid userId, DateOnly from, DateOnly to, double capacityHours, CancellationToken ct = default)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var windowStart = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var windowEnd = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var blocks = await db.Blocks
            .Where(b => b.OwnerId == userId
                && b.Status != BlockStatus.Skipped
                && b.Status != BlockStatus.Missed
                && b.StartUtc < windowEnd
                && b.EndUtc > windowStart)
            .Select(b => new { b.StartUtc, b.EndUtc })
            .ToListAsync(ct);

        var loads = new List<DayLoad>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var dayEnd = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var minutes = blocks.Sum(b =>
            {
                var clippedStart = b.StartUtc > dayStart ? b.StartUtc : dayStart;
                var clippedEnd = b.EndUtc < dayEnd ? b.EndUtc : dayEnd;
                return Math.Max(0, (clippedEnd - clippedStart).TotalMinutes);
            });
            var hours = minutes / 60.0;
            loads.Add(new DayLoad(day, Math.Round(hours, 2), capacityHours, hours > capacityHours));
        }

        return loads;
    }
}
