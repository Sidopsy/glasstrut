using Glasstrut.Api.Models.Dtos;

namespace Glasstrut.Api.Services;

public interface IFamilyService
{
    Task<FamilyDto> CreateFamilyAsync(string userId, string name);
    Task<FamilyDto> JoinFamilyAsync(string userId, string inviteCode);
    Task<FamilyDto> GetFamilyAsync(string userId, Guid familyId);
    Task<List<FamilyDto>> GetUserFamiliesAsync(string userId);
    Task RemoveMemberAsync(string userId, Guid familyId, string memberUserId);
}
