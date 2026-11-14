namespace TimeBlock.Core.Entities;

public enum BlockPrivacy
{
    Details = 0,
    BusyOnly = 1
}

public enum BlockStatus
{
    Planned = 0,
    Done = 1,
    PartiallyDone = 2,
    Skipped = 3,
    Missed = 4
}

public class BlockTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid? GroupId { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public BlockPrivacy Privacy { get; set; } = BlockPrivacy.Details;
    public string Rrule { get; set; } = "FREQ=WEEKLY";
    public TimeOnly StartTime { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public DateOnly StartDate { get; set; }
    public DateOnly? UntilDate { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Block
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? TemplateId { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public BlockPrivacy Privacy { get; set; } = BlockPrivacy.Details;
    public BlockStatus Status { get; set; } = BlockStatus.Planned;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? BlockId { get; set; }
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class NotificationTypes
{
    public const string Reminder = "reminder";
    public const string Missed = "missed";
    public const string GroupMissed = "group-missed";
}
