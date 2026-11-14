using Microsoft.EntityFrameworkCore;
using TimeBlock.Core.Data;
using TimeBlock.Core.Entities;
using TimeBlock.Core.Recurrence;

namespace TimeBlock.Api.Services;

public static class BlockMaterializer
{
    public static readonly TimeSpan Horizon = TimeSpan.FromDays(14);

    public static async Task MaterializeAsync(TimeBlockDbContext db, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = DateOnly.FromDateTime(now);
        var windowEnd = windowStart.AddDays(14);
        var earliest = now - TimeSpan.FromDays(1);

        var templates = await db.Templates.AsNoTracking().ToListAsync(ct);
        var pending = new List<Block>();

        foreach (var template in templates)
        {
            var timeZone = ResolveTimeZone(template.TimeZoneId);
            foreach (var date in RecurrenceEngine.Expand(template.Rrule, template.StartDate, windowStart, windowEnd))
            {
                var localStart = date.ToDateTime(template.StartTime);
                DateTime startUtc;
                try
                {
                    startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), timeZone);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (startUtc < earliest)
                {
                    continue;
                }

                pending.Add(new Block
                {
                    OwnerId = template.OwnerId,
                    GroupId = template.GroupId,
                    TemplateId = template.Id,
                    Title = template.Title,
                    Category = template.Category,
                    Privacy = template.Privacy,
                    StartUtc = startUtc,
                    EndUtc = startUtc.AddMinutes(template.DurationMinutes)
                });
            }
        }

        if (pending.Count == 0)
        {
            return;
        }

        var existingKeys = await db.Blocks
            .Where(b => b.TemplateId != null)
            .Select(b => new { b.TemplateId, b.StartUtc })
            .ToListAsync(ct);
        var existing = existingKeys.Select(k => (k.TemplateId!.Value, k.StartUtc)).ToHashSet();

        foreach (var block in pending.Where(b => !existing.Contains((b.TemplateId!.Value, b.StartUtc))))
        {
            db.Blocks.Add(block);
        }

        await db.SaveChangesAsync(ct);
    }

    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
