namespace TimeBlock.Core.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public double DailyCapacityHours { get; set; } = 8;
    public bool AccountabilityOptIn { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<GroupMember> Memberships { get; set; } = new();
}

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string InviteCode { get; set; } = "";
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<GroupMember> Members { get; set; } = new();
}

public class GroupMember
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Role { get; set; } = MemberRoles.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public static class MemberRoles
{
    public const string Owner = "Owner";
    public const string Member = "Member";
}
