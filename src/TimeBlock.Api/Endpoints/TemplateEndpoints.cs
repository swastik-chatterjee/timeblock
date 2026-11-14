using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeBlock.Api.Services;
using TimeBlock.Core.Contracts;
using TimeBlock.Core.Data;
using TimeBlock.Core.Entities;
using TimeBlock.Core.Recurrence;

namespace TimeBlock.Api.Endpoints;

public static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/templates").WithTags("Templates").RequireAuthorization();

        group.MapGet("/", async (CurrentUser me, TimeBlockDbContext db) =>
        {
            var templates = await db.Templates
                .Where(t => t.OwnerId == me.UserId)
                .OrderBy(t => t.StartDate)
                .ToListAsync();
            return TypedResults.Ok(templates.Select(ToDto).ToList());
        });

        group.MapPost("/", async Task<Results<Ok<TemplateDto>, BadRequest<string>, NotFound>> (
                CreateTemplateRequest request,
                CurrentUser me,
                TimeBlockDbContext db,
                TimelineNotifier notifier) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return TypedResults.BadRequest("Title is required.");
            }

            if (request.DurationMinutes <= 0)
            {
                return TypedResults.BadRequest("Duration must be positive.");
            }

            try
            {
                RecurrenceEngine.Parse(request.Rrule);
            }
            catch
            {
                return TypedResults.BadRequest("Invalid recurrence rule.");
            }

            if (request.GroupId.HasValue && !await db.GroupMembers.AnyAsync(m => m.GroupId == request.GroupId.Value && m.UserId == me.UserId))
            {
                return TypedResults.NotFound();
            }

            var template = new BlockTemplate
            {
                OwnerId = me.UserId,
                GroupId = request.GroupId,
                Title = request.Title.Trim(),
                Category = request.Category?.Trim() ?? "",
                Privacy = request.Privacy,
                Rrule = request.Rrule.Trim().ToUpperInvariant(),
                StartTime = request.StartTime,
                DurationMinutes = request.DurationMinutes,
                StartDate = request.StartDate,
                UntilDate = request.UntilDate,
                TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "UTC" : request.TimeZoneId.Trim()
            };
            db.Templates.Add(template);
            await db.SaveChangesAsync();

            await BlockMaterializer.MaterializeAsync(db);

            var created = await db.Blocks
                .Where(b => b.TemplateId == template.Id)
                .OrderBy(b => b.StartUtc)
                .ToListAsync();
            foreach (var block in created)
            {
                await notifier.BlockChanged(block.GroupId, "created", BlockMapper.ToDto(block, template.Title, me.UserId));
            }

            return TypedResults.Ok(ToDto(template));
        });

        group.MapDelete("/{id:guid}", async Task<Results<Ok, NotFound>> (Guid id, CurrentUser me, TimeBlockDbContext db) =>
        {
            var template = await db.Templates.FindAsync(id);
            if (template is null || template.OwnerId != me.UserId)
            {
                return TypedResults.NotFound();
            }

            var cutoff = DateTime.UtcNow;
            var future = await db.Blocks
                .Where(b => b.TemplateId == id && b.Status == BlockStatus.Planned && b.StartUtc > cutoff)
                .ToListAsync();
            db.Blocks.RemoveRange(future);
            db.Templates.Remove(template);
            await db.SaveChangesAsync();
            return TypedResults.Ok();
        });
    }

    private static TemplateDto ToDto(BlockTemplate template) => new(
        template.Id,
        template.Title,
        template.Category,
        template.Privacy,
        template.GroupId,
        template.Rrule,
        template.StartTime,
        template.DurationMinutes,
        template.StartDate,
        template.UntilDate,
        template.TimeZoneId);
}
