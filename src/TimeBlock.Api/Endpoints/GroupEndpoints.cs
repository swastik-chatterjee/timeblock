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

public static class GroupEndpoints
{
    public static void MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/groups").WithTags("Groups").RequireAuthorization();

        group.MapPost("/", async Task<Results<Ok<GroupDto>, BadRequest<string>>> (
                CreateGroupRequest request,
                CurrentUser me,
                TimeBlockDbContext db) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return TypedResults.BadRequest("Group name is required.");
                }

                var entity = new Group
                {
                    Name = request.Name.Trim(),
                    InviteCode = InviteCodeGenerator.NewCode(),
                    CreatedById = me.UserId
                };
                db.Groups.Add(entity);
                db.GroupMembers.Add(new GroupMember { GroupId = entity.Id, UserId = me.UserId, Role = MemberRoles.Owner });
                await db.SaveChangesAsync();
                return TypedResults.Ok(await GroupMapper.ToDtoAsync(entity, db));
            });

        group.MapPost("/join", async Task<Results<Ok<GroupDto>, NotFound, Conflict<string>>> (
                JoinGroupRequest request,
                CurrentUser me,
                TimeBlockDbContext db) =>
            {
                var code = request.InviteCode.Trim().ToUpperInvariant();
                var target = await db.Groups.FirstOrDefaultAsync(g => g.InviteCode == code);
                if (target is null)
                {
                    return TypedResults.NotFound();
                }

                var already = await db.GroupMembers.AnyAsync(m => m.GroupId == target.Id && m.UserId == me.UserId);
                if (already)
                {
                    return TypedResults.Conflict("You are already a member of this group.");
                }

                db.GroupMembers.Add(new GroupMember { GroupId = target.Id, UserId = me.UserId });
                await db.SaveChangesAsync();
                return TypedResults.Ok(await GroupMapper.ToDtoAsync(target, db));
            });

        group.MapGet("/mine", async (CurrentUser me, TimeBlockDbContext db) =>
        {
            var groupIds = await db.GroupMembers
                .Where(m => m.UserId == me.UserId)
                .Select(m => m.GroupId)
                .ToListAsync();
            var result = new List<GroupDto>();
            foreach (var groupId in groupIds)
            {
                var entity = await db.Groups.FindAsync(groupId);
                if (entity is not null)
                {
                    result.Add(await GroupMapper.ToDtoAsync(entity, db));
                }
            }

            return TypedResults.Ok(result);
        });

        group.MapPost("/{groupId:guid}/regenerate-invite", async Task<Results<Ok<string>, NotFound, ForbidHttpResult>> (
                Guid groupId,
                CurrentUser me,
                TimeBlockDbContext db) =>
        {
            var entity = await db.Groups.FindAsync(groupId);
            if (entity is null)
            {
                return TypedResults.NotFound();
            }

            var isOwner = await db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == me.UserId && m.Role == MemberRoles.Owner);
            if (!isOwner)
            {
                return TypedResults.Forbid();
            }

            entity.InviteCode = InviteCodeGenerator.NewCode();
            await db.SaveChangesAsync();
            return TypedResults.Ok(entity.InviteCode);
        });

        group.MapPost("/{groupId:guid}/leave", async Task<Results<Ok, NotFound>> (Guid groupId, CurrentUser me, TimeBlockDbContext db) =>
        {
            var membership = await db.GroupMembers.FindAsync(groupId, me.UserId);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            db.GroupMembers.Remove(membership);
            await db.SaveChangesAsync();
            return TypedResults.Ok();
        });

        group.MapGet("/{groupId:guid}/timeline", async Task<Results<Ok<GroupTimelineDto>, NotFound>> (
                Guid groupId,
                DateOnly? weekStart,
                CurrentUser me,
                TimeBlockDbContext db) =>
        {
            var isMember = await db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == me.UserId);
            if (!isMember)
            {
                return TypedResults.NotFound();
            }

            var entity = await db.Groups.FindAsync(groupId);
            if (entity is null)
            {
                return TypedResults.NotFound();
            }

            var start = weekStart ?? RecurrenceEngine.StartOfWeek(DateOnly.FromDateTime(DateTime.UtcNow));
            var from = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var to = start.AddDays(7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            var members = await db.GroupMembers
                .Where(m => m.GroupId == groupId)
                .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m.Role, u.Id, u.DisplayName })
                .OrderBy(m => m.Role)
                .ToListAsync();
            var memberDtos = members.Select(m => new GroupMemberDto(m.Id, m.DisplayName, m.Role)).ToList();

            var rows = await db.Blocks
                .Where(b => b.GroupId == groupId && b.StartUtc < to && b.EndUtc > from)
                .Join(db.Users, b => b.OwnerId, u => u.Id, (b, u) => new { b, u.DisplayName })
                .OrderBy(r => r.b.StartUtc)
                .ToListAsync();

            var blocks = rows.Select(r => BlockMapper.ToDto(r.b, r.DisplayName, me.UserId)).ToList();
            return TypedResults.Ok(new GroupTimelineDto(new GroupDto(entity.Id, entity.Name, entity.InviteCode, memberDtos), blocks));
        });
    }
}
