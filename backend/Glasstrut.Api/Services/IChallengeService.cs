using Glasstrut.Api.Models.Dtos;

namespace Glasstrut.Api.Services;

public interface IChallengeService
{
    Task<ChallengeDto> CreateChallengeAsync(string userId, CreateChallengeRequest request);
    Task<ChallengeDto> GetChallengeAsync(string userId, Guid challengeId);
    Task<List<ChallengeDto>> GetChallengesAsync(string userId, Guid? familyId);
}
