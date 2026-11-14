using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeBlock.Api.Services;
using TimeBlock.Core.Contracts;
using TimeBlock.Core.Data;
using TimeBlock.Core.Services;
using TimeBlock.Core.Entities;

namespace TimeBlock.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/", async (CurrentUser me, TimeBlockDbContext db) =>
        {
            var notifications = await db.Notifications
                .Where(n => n.UserId == me.UserId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .ToListAsync();
            var dtos = notifications.Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.BlockId, n.IsRead, n.CreatedAt)).ToList();
            return TypedResults.Ok(dtos);
        });

        group.MapPost("/{id:guid}/read", async Task<Results<Ok, NotFound>> (Guid id, CurrentUser me, TimeBlockDbContext db) =>
        {
            var notification = await db.Notifications.FindAsync(id);
            if (notification is null || notification.UserId != me.UserId)
            {
                return TypedResults.NotFound();
            }

            notification.IsRead = true;
            await db.SaveChangesAsync();
            return TypedResults.Ok();
        });

        group.MapPost("/read-all", async (CurrentUser me, TimeBlockDbContext db) =>
        {
            await db.Notifications
                .Where(n => n.UserId == me.UserId && !n.IsRead)
                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
            return TypedResults.Ok();
        });
    }
}
