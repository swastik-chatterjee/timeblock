using Microsoft.EntityFrameworkCore;
using TimeBlock.Core.Contracts;
using TimeBlock.Core.Data;
using TimeBlock.Core.Entities;

namespace TimeBlock.Api.Services;

public static class GroupMapper
{
    public static async Task<GroupDto> ToDtoAsync(Group group, TimeBlockDbContext db)
    {
        var members = await db.GroupMembers
            .Where(m => m.GroupId == group.Id)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m.Role, u.Id, u.DisplayName })
            .OrderBy(m => m.Role)
            .ToListAsync();
        return new GroupDto(group.Id, group.Name, group.InviteCode,
            members.Select(m => new GroupMemberDto(m.Id, m.DisplayName, m.Role)).ToList());
    }
}
