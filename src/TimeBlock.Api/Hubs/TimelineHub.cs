using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TimeBlock.Core.Data;

namespace TimeBlock.Api.Hubs;

[Authorize]
public class TimelineHub(TimeBlockDbContext db) : Hub
{
    public async Task Subscribe(Guid[] groupIds)
    {
        var userId = Context.User?.FindFirstValue("sub");
        if (!Guid.TryParse(userId, out var id))
        {
            return;
        }

        var memberOf = await db.GroupMembers
            .Where(m => m.UserId == id && groupIds.Contains(m.GroupId))
            .Select(m => m.GroupId)
            .ToListAsync();

        foreach (var groupId in memberOf)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, Services.TimelineNotifier.GroupName(groupId));
        }
    }
}
