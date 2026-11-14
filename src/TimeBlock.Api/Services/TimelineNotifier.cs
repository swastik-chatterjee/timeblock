using Microsoft.AspNetCore.SignalR;
using TimeBlock.Core.Contracts;

namespace TimeBlock.Api.Services;

public class TimelineNotifier(IHubContext<Hubs.TimelineHub> hub)
{
    public Task BlockChanged(Guid? groupId, string action, BlockDto block)
    {
        if (!groupId.HasValue)
        {
            return Task.CompletedTask;
        }

        return hub.Clients.Group(GroupName(groupId.Value)).SendAsync("blockChanged", new BlockChangedEvent(action, block));
    }

    public static string GroupName(Guid groupId) => $"group-{groupId}";
}
