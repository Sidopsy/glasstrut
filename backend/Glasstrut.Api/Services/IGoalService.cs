using Glasstrut.Api.Models.Dtos;

namespace Glasstrut.Api.Services;

public interface IGoalService
{
    Task<LogActivityResponse> LogActivityAsync(string userId, Guid challengeId, Guid activityId, LogProgressRequest request);
    Task<ProgressAndAchievementsDto> GetChallengeProgressAsync(string userId, Guid challengeId);
    Task<ChallengeProgressMembersDto> GetChallengeProgressMembersAsync(string userId, Guid challengeId);
    Task<List<ActivityLogEntryDto>> GetActivityLogAsync(string userId, Guid challengeId, int count = 20);
    Task<List<AchievementDto>> GetUserAchievementsAsync(string userId);
}
