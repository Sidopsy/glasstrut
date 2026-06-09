namespace Glasstrut.Api.Models.Dtos;

public record RecordProgressRequest(decimal CurrentValue);

public record GoalProgressDto(
    Guid Id,
    Guid GoalId,
    string GoalDescription,
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
