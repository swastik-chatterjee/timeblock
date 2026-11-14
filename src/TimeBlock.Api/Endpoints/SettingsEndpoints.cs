using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeBlock.Api.Services;
using TimeBlock.Core.Contracts;
using TimeBlock.Core.Data;

namespace TimeBlock.Api.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me").WithTags("Me").RequireAuthorization();

        group.MapGet("/", async (CurrentUser me, TimeBlockDbContext db) =>
        {
            var user = await db.Users.FindAsync(me.UserId);
            return user is null ? Results.NotFound() : Results.Ok(user.ToUserDto());
        });

        group.MapPut("/", async Task<Results<Ok<UserDto>, NotFound, BadRequest<string>>> (
                UpdateSettingsRequest request,
                CurrentUser me,
                TimeBlockDbContext db) =>
        {
            if (request.DailyCapacityHours is < 1 or > 24)
            {
                return TypedResults.BadRequest("Daily capacity must be between 1 and 24 hours.");
            }

            var user = await db.Users.FindAsync(me.UserId);
            if (user is null)
            {
                return TypedResults.NotFound();
            }

            user.DailyCapacityHours = request.DailyCapacityHours;
            user.AccountabilityOptIn = request.AccountabilityOptIn;
            await db.SaveChangesAsync();
            return TypedResults.Ok(user.ToUserDto());
        });
    }
}
