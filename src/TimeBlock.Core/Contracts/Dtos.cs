using TimeBlock.Core.Entities;

namespace TimeBlock.Core.Contracts;

public record UserDto(Guid Id, string Email, string DisplayName, double DailyCapacityHours, bool AccountabilityOptIn);

public record AuthResponse(string Token, UserDto User);

public record RegisterRequest(string Email, string DisplayName, string Password, string? InviteCode);

public record LoginRequest(string Email, string Password);

public record UpdateSettingsRequest(double DailyCapacityHours, bool AccountabilityOptIn);

public record GroupMemberDto(Guid UserId, string DisplayName, string Role);

public record GroupDto(Guid Id, string Name, string InviteCode, IReadOnlyList<GroupMemberDto> Members);

public record CreateGroupRequest(string Name);

public record JoinGroupRequest(string InviteCode);

public record GroupTimelineDto(GroupDto Group, IReadOnlyList<BlockDto> Blocks);

public record BlockDto(
    Guid Id,
    Guid OwnerId,
    string OwnerName,
    Guid? GroupId,
    Guid? TemplateId,
    string Title,
    string Category,
    BlockPrivacy Privacy,
    BlockStatus Status,
    DateTime StartUtc,
    DateTime EndUtc,
    DateTime? ConfirmedAt);

public record CreateBlockRequest(string Title, string Category, BlockPrivacy Privacy, DateTime StartUtc, DateTime EndUtc, Guid? GroupId);

public record UpdateBlockStatusRequest(BlockStatus Status);

public record BlockChangedEvent(string Action, BlockDto Block);

public record TemplateDto(
    Guid Id,
    string Title,
    string Category,
    BlockPrivacy Privacy,
    Guid? GroupId,
    string Rrule,
    TimeOnly StartTime,
    int DurationMinutes,
    DateOnly StartDate,
    DateOnly? UntilDate,
    string TimeZoneId);

public record CreateTemplateRequest(
    string Title,
    string Category,
    BlockPrivacy Privacy,
    Guid? GroupId,
    string Rrule,
    TimeOnly StartTime,
    int DurationMinutes,
    DateOnly StartDate,
    DateOnly? UntilDate,
    string? TimeZoneId);

public record DayLoad(DateOnly Date, double PlannedHours, double CapacityHours, bool Overloaded);

public record WeekFollowThroughDto(DateOnly WeekStart, int Done, int PartiallyDone, int Skipped, int Missed, double Rate);

public record NotificationDto(Guid Id, string Type, string Message, Guid? BlockId, bool IsRead, DateTime CreatedAt);
