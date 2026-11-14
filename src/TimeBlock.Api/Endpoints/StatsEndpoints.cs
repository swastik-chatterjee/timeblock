using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeBlock.Api.Services;
using TimeBlock.Core.Contracts;
using TimeBlock.Core.Data;
using TimeBlock.Core.Services;
using TimeBlock.Core.Entities;

namespace TimeBlock.Api.Endpoints;

public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stats").WithTags("Stats").RequireAuthorization();

        group.MapGet("/follow-through", async Task<Results<Ok<List<WeekFollowThroughDto>>, NotFound>> (CurrentUser me, TimeBlockDbContext db, TimeBlockStats stats, int? weeks) =>
        {
            var user = await db.Users.FindAsync(me.UserId);
            if (user is null)
            {
                return TypedResults.NotFound();
            }

            var result = await stats.WeeklyFollowThroughAsync(me.UserId, weeks ?? 6);
            return TypedResults.Ok(result);
        });
    }
}
