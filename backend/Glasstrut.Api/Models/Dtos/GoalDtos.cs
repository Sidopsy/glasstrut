namespace Glasstrut.Api.Models.Dtos;

public record LogProgressRequest(
    decimal Amount,
    decimal? TimeAmount = null,
    string? Notes = null,
    DateTime? ClientRecordedAt = null
);

public record LogActivityResponse(
    GoalProgressDto? Progress,
    SurpriseDto? Surprise = null,
    decimal? CurrencyEarned = null
);

public record SurpriseDto(
    string Title,
    string Description
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
    DateTime? CompletedAt,
    bool IsPerEntry = false
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
    List<AchievementDto> Achievements,
    decimal CurrencyBalance = 0,
    int CurrentStreak = 0,
    string? CurrencyName = null
);

public record MemberProgressDto(
    string UserId,
    string Email,
    List<GoalProgressDto> Goals,
    decimal CurrencyBalance = 0,
    int CurrentStreak = 0,
    string? CurrencyName = null
);

public record ChallengeProgressMembersDto(
    List<MemberProgressDto> Members,
    List<AchievementDto> Achievements
);

public record ActivityLogEntryDto(
    Guid Id,
    string UserEmail,
    string ActivityName,
    string? GoalDescription,
    string? GoalType,
    decimal Amount,
    decimal? TimeAmount,
    string? Unit,
    string? TimeUnit,
    string? Notes,
    DateTime RecordedAt,
    decimal? CurrencyEarned = null,
    Guid? ActivityId = null
);

public record ChronicleEntryDto(
    Guid Id,
    string UserEmail,
    string Type,
    string? ActivityName,
    decimal? Amount,
    string? Unit,
    decimal? CurrencyEarned,
    string? PrizeDescription,
    decimal? Cost,
    DateTime RecordedAt
);
