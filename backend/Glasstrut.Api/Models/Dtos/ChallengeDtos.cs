namespace Glasstrut.Api.Models.Dtos;

public record CreateGoalDto(
    string Description,
    decimal? TargetValue = null,
    string? Unit = null,
    decimal? PointValue = null
);

public record CreatePrizeDto(
    string Description,
    decimal? Cost = null
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
    string? CurrencyName = null
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
    List<ChallengeGoalDto> Goals,
    List<ChallengePrizeDto> Prizes,
    List<string> TargetUserIds
);

public record ChallengeGoalDto(
    Guid Id,
    string Description,
    decimal? TargetValue,
    string? Unit,
    decimal? PointValue
);

public record ChallengePrizeDto(
    Guid Id,
    string Description,
    decimal? Cost
);
