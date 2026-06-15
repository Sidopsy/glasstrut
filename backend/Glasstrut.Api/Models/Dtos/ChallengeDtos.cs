namespace Glasstrut.Api.Models.Dtos;

public record CreateActivityDto(
    string Name,
    string Unit,
    decimal PointValue,
    string? TimeUnit = null,
    string ActivityType = "Occurrence",
    List<int>? GoalIndices = null
);

public record UpdateActivityDto(
    Guid? Id,
    string Name,
    string Unit,
    decimal PointValue,
    string? TimeUnit = null,
    string ActivityType = "Occurrence",
    List<Guid>? GoalIds = null
);

public record CreateGoalDto(
    string Description,
    string Type = "Achievement",
    decimal? TargetValue = null,
    string? Unit = null,
    bool IsHidden = false,
    bool IsPerEntry = false,
    List<CreateActivityDto>? Activities = null
);

public record CreatePrizeDto(
    string Description,
    decimal? Cost = null,
    bool HasQR = true,
    Guid? ChallengeGoalId = null
);

public record CreateChallengeRequest(
    string Title,
    string Description,
    string Type,
    Guid? FamilyId,
    DateTime? StartDate,
    DateTime? EndDate,
    List<CreateGoalDto>? Goals,
    List<CreatePrizeDto>? Prizes,
    List<string>? TargetUserIds,
    string? CurrencyName = null,
    List<CreateActivityDto>? Activities = null
);

public record UpdateGoalDto(
    Guid? Id,
    string Description,
    string Type = "Achievement",
    decimal? TargetValue = null,
    string? Unit = null,
    bool IsHidden = false,
    bool IsPerEntry = false,
    List<UpdateActivityDto>? Activities = null
);

public record UpdatePrizeDto(
    Guid? Id,
    string Description,
    decimal? Cost = null,
    bool HasQR = true,
    Guid? ChallengeGoalId = null
);

public record UpdateChallengeRequest(
    string Title,
    string Description,
    DateTime? StartDate,
    DateTime? EndDate,
    List<UpdateGoalDto>? Goals,
    List<UpdatePrizeDto>? Prizes,
    string? CurrencyName = null,
    List<UpdateActivityDto>? Activities = null
);

public record ChallengeDto(
    Guid Id,
    string Title,
    string Description,
    string Type,
    Guid? FamilyId,
    DateTime? StartDate,
    DateTime? EndDate,
    DateTime CreatedAt,
    string? CurrencyName,
    string CreatedById,
    List<ChallengeGoalDto> Goals,
    List<ChallengePrizeDto> Prizes,
    List<string> TargetUserIds,
    List<ChallengeActivityDto> Activities
);

public record ChallengeActivityDto(
    Guid Id,
    string Name,
    string ActivityType,
    string Unit,
    string? TimeUnit,
    decimal PointValue,
    List<Guid>? GoalIds = null
);

public record ChallengeGoalDto(
    Guid Id,
    string Description,
    string Type,
    decimal? TargetValue,
    string? Unit,
    bool IsHidden,
    bool IsPerEntry = false
);

public record ChallengePrizeDto(
    Guid Id,
    string Description,
    decimal? Cost,
    bool HasQR,
    Guid? ChallengeGoalId
);
