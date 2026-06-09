using Glasstrut.Api.Models.Dtos;

namespace Glasstrut.Api.Services;

public interface IGoalService
{
    Task<GoalProgressDto> RecordProgressAsync(string userId, Guid challengeGoalId, RecordProgressRequest request);
    Task<ProgressAndAchievementsDto> GetChallengeProgressAsync(string userId, Guid challengeId);
    Task<List<AchievementDto>> GetUserAchievementsAsync(string userId);
}
