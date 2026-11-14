using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeBlock.Api.Services;
using TimeBlock.Core.Contracts;
using TimeBlock.Core.Data;
using TimeBlock.Core.Entities;
using TimeBlock.Core.Recurrence;
using TimeBlock.Core.Services;

namespace TimeBlock.Api.Endpoints;

public static class BlockEndpoints
{
    public static void MapBlockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/blocks").WithTags("Blocks").RequireAuthorization();

        group.MapGet("/", async (CurrentUser me, TimeBlockDbContext db, DateTime? from, DateTime? to) =>
        {
            var start = from ?? DateTime.UtcNow.AddDays(-7);
            var end = to ?? DateTime.UtcNow.AddDays(14);
            var rows = await db.Blocks
                .Where(b => b.OwnerId == me.UserId && b.StartUtc < end && b.EndUtc > start)
                .Join(db.Users, b => b.OwnerId, u => u.Id, (b, u) => new { b, u.DisplayName })
                .OrderBy(r => r.b.StartUtc)
                .ToListAsync();
            return TypedResults.Ok(rows.Select(r => BlockMapper.ToDto(r.b, r.DisplayName, me.UserId)).ToList());
        });

        group.MapPost("/", async Task<Results<Ok<BlockDto>, BadRequest<string>, NotFound>> (
                CreateBlockRequest request,
                CurrentUser me,
                TimeBlockDbContext db,
                TimelineNotifier notifier) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return TypedResults.BadRequest("Title is required.");
            }

            if (request.EndUtc <= request.StartUtc)
            {
                return TypedResults.BadRequest("End must be after start.");
            }

            if (request.GroupId.HasValue && !await db.GroupMembers.AnyAsync(m => m.GroupId == request.GroupId.Value && m.UserId == me.UserId))
            {
                return TypedResults.NotFound();
            }

            var block = new Block
            {
                OwnerId = me.UserId,
                GroupId = request.GroupId,
                Title = request.Title.Trim(),
                Category = request.Category?.Trim() ?? "",
                Privacy = request.Privacy,
                StartUtc = request.StartUtc,
                EndUtc = request.EndUtc
            };
            db.Blocks.Add(block);
            await db.SaveChangesAsync();

            var ownerName = await db.Users.Where(u => u.Id == me.UserId).Select(u => u.DisplayName).FirstAsync();
            var dto = BlockMapper.ToDto(block, ownerName, me.UserId);
            await notifier.BlockChanged(block.GroupId, "created", dto);
            return TypedResults.Ok(dto);
        });

        group.MapPatch("/{id:guid}/status", async Task<Results<Ok<BlockDto>, NotFound, BadRequest<string>>> (
                Guid id,
                UpdateBlockStatusRequest request,
                CurrentUser me,
                TimeBlockDbContext db,
                TimelineNotifier notifier) =>
        {
            var block = await db.Blocks.FindAsync(id);
            if (block is null)
            {
                return TypedResults.NotFound();
            }

            if (block.OwnerId != me.UserId)
            {
                return TypedResults.BadRequest("Only the owner can confirm a block.");
            }

            if (request.Status is BlockStatus.Planned or BlockStatus.Missed)
            {
                return TypedResults.BadRequest("Blocks can be confirmed as done, partially done, or skipped.");
            }

            block.Status = request.Status;
            block.ConfirmedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var ownerName = await db.Users.Where(u => u.Id == me.UserId).Select(u => u.DisplayName).FirstAsync();
            var dto = BlockMapper.ToDto(block, ownerName, me.UserId);
            await notifier.BlockChanged(block.GroupId, "updated", dto);
            return TypedResults.Ok(dto);
        });

        group.MapDelete("/{id:guid}", async Task<Results<Ok, NotFound>> (Guid id, CurrentUser me, TimeBlockDbContext db, TimelineNotifier notifier) =>
        {
            var block = await db.Blocks.FindAsync(id);
            if (block is null)
            {
                return TypedResults.NotFound();
            }

            if (block.OwnerId != me.UserId)
            {
                return TypedResults.NotFound();
            }

            var ownerName = await db.Users.Where(u => u.Id == me.UserId).Select(u => u.DisplayName).FirstAsync();
            var dto = BlockMapper.ToDto(block, ownerName, me.UserId);
            db.Blocks.Remove(block);
            await db.SaveChangesAsync();
            await notifier.BlockChanged(block.GroupId, "deleted", dto);
            return TypedResults.Ok();
        });

        group.MapGet("/overload", async (CurrentUser me, TimeBlockDbContext db, TimeBlockStats stats, DateOnly? from, DateOnly? to) =>
        {
            var user = await db.Users.FindAsync(me.UserId);
            var start = from ?? RecurrenceEngine.StartOfWeek(DateOnly.FromDateTime(DateTime.UtcNow));
            var end = to ?? start.AddDays(6);
            var loads = await stats.DailyLoadAsync(me.UserId, start, end, user?.DailyCapacityHours ?? 8);
            return TypedResults.Ok(loads);
        });
    }
}
