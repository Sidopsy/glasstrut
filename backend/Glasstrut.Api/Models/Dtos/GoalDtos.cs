namespace Glasstrut.Api.Models.Dtos;

public record LogProgressRequest(
    decimal Amount,
    string? Notes = null
);

public record GoalProgressDto(
    Guid Id,
    Guid GoalId,
    string GoalDescription,
    string GoalType,
    decimal? TargetValue,
    string? Unit,
    decimal CurrentValue,
    bool IsCompleted,
    DateTime? CompletedAt
);

public record AchievementDto(
    Guid Id,
    string Title,
    string Description,
    bool IsHidden,
    DateTime CreatedAt,
    DateTime? UnlockedAt
);

public record ProgressAndAchievementsDto(
    List<GoalProgressDto> Progress,
    List<AchievementDto> Achievements
);

public record MemberProgressDto(
    string UserId,
    string Email,
    List<GoalProgressDto> Goals
);

public record ChallengeProgressMembersDto(
    List<MemberProgressDto> Members,
    List<AchievementDto> Achievements
);

public record ActivityLogEntryDto(
    Guid Id,
    string UserEmail,
    string ActivityName,
    string GoalDescription,
    string GoalType,
    decimal Amount,
    string? Unit,
    string? Notes,
    DateTime RecordedAt
);
