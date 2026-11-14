using Microsoft.EntityFrameworkCore;
using TimeBlock.Core.Data;
using TimeBlock.Core.Entities;

namespace TimeBlock.Api.Services;

public class BlockWorker(IServiceScopeFactory scopeFactory, ILogger<BlockWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReminderGrace = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(6);
    private static readonly TimeSpan MissedGrace = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan MissedWindow = TimeSpan.FromDays(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await RunOnceAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "TimeBlock worker cycle failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task RunOnceAsync(IServiceProvider provider, CancellationToken ct)
    {
        var db = provider.GetRequiredService<TimeBlockDbContext>();
        var notifier = provider.GetRequiredService<TimelineNotifier>();
        await BlockMaterializer.MaterializeAsync(db, ct);
        await RemindAsync(db, ct);
        await MarkMissedAsync(db, notifier, ct);
    }

    private static async Task RemindAsync(TimeBlockDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var due = await db.Blocks
            .Where(b => b.Status == BlockStatus.Planned
                && b.EndUtc <= now - ReminderGrace
                && b.EndUtc > now - ReminderWindow
                && !db.Notifications.Any(n => n.BlockId == b.Id && n.Type == NotificationTypes.Reminder))
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            return;
        }

        foreach (var block in due)
        {
            db.Notifications.Add(new Notification
            {
                UserId = block.OwnerId,
                BlockId = block.Id,
                Type = NotificationTypes.Reminder,
                Message = $"\"{block.Title}\" ended a while ago. Mark it done, partially done, or skipped to keep your follow-through honest."
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task MarkMissedAsync(TimeBlockDbContext db, TimelineNotifier notifier, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var candidates = await db.Blocks
            .Where(b => b.Status == BlockStatus.Planned
                && b.EndUtc <= now - MissedGrace
                && b.EndUtc > now - MissedWindow)
            .Join(db.Users,
                b => b.OwnerId,
                u => u.Id,
                (b, u) => new { Block = b, u.AccountabilityOptIn, u.DisplayName })
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            var block = candidate.Block;
            block.Status = BlockStatus.Missed;

            db.Notifications.Add(new Notification
            {
                UserId = block.OwnerId,
                BlockId = block.Id,
                Type = NotificationTypes.Missed,
                Message = $"Missed: \"{block.Title}\". It now counts against this week's follow-through rate."
            });

            if (candidate.AccountabilityOptIn && block.GroupId.HasValue)
            {
                var memberIds = await db.GroupMembers
                    .Where(m => m.GroupId == block.GroupId && m.UserId != block.OwnerId)
                    .Select(m => m.UserId)
                    .ToListAsync(ct);

                foreach (var memberId in memberIds)
                {
                    db.Notifications.Add(new Notification
                    {
                        UserId = memberId,
                        BlockId = block.Id,
                        Type = NotificationTypes.GroupMissed,
                        Message = $"{candidate.DisplayName} missed \"{block.Title}\"."
                    });
                }

                await notifier.BlockChanged(block.GroupId, "missed", BlockMapper.ToDto(block, candidate.DisplayName));
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
