namespace Glasstrut.Api.Models.Dtos;

public record CreateChallengeRequest(
    string Title,
    string Description,
    string Type,
    Guid? FamilyId,
    DateTime? StartDate,
    DateTime? EndDate,
    List<string>? Goals,
    List<string>? Prizes,
    List<string>? TargetUserIds
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
    List<ChallengeGoalDto> Goals,
    List<ChallengePrizeDto> Prizes,
    List<string> TargetUserIds
);

public record ChallengeGoalDto(Guid Id, string Description);

public record ChallengePrizeDto(Guid Id, string Description);
