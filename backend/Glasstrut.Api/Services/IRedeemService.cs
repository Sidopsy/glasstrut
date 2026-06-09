using Glasstrut.Api.Models.Dtos;

namespace Glasstrut.Api.Services;

public interface IRedeemService
{
    Task<byte[]> GetPrizeQrAsync(string userId, Guid challengeId, Guid prizeId);
    Task<PrizeRedeemResponse> RedeemPrizeAsync(string userId, Guid challengeId, Guid prizeId);
    Task<List<PrizeClaimDto>> GetPrizeClaimsAsync(string userId, Guid challengeId);
}
