using Microsoft.EntityFrameworkCore;
using QRCoder;
using Glasstrut.Api.Data;
using Glasstrut.Api.Models;
using Glasstrut.Api.Models.Dtos;

namespace Glasstrut.Api.Services;

public class RedeemService : IRedeemService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RedeemService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<byte[]> GetPrizeQrAsync(string userId, Guid challengeId, Guid prizeId)
    {
        var challenge = await _db.Challenges
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException("Challenge not found.");

        await VerifyAccessAsync(userId, challenge);

        var prize = await _db.ChallengePrizes
            .FirstOrDefaultAsync(p => p.Id == prizeId && p.ChallengeId == challengeId)
            ?? throw new InvalidOperationException("Prize not found.");

        if (!prize.HasQR)
            throw new InvalidOperationException("QR codes are not enabled for this prize.");

        var baseUrl = GetBaseUrl();
        var payload = $"{baseUrl}/?claim={challengeId}:{prizeId}";

        using var generator = new QRCodeGenerator();
        var qrData = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(qrData);
        return png.GetGraphic(20);
    }

    public async Task<PrizeRedeemResponse> RedeemPrizeAsync(string userId, Guid challengeId, Guid prizeId)
    {
        var challenge = await _db.Challenges
            .Include(c => c.Goals)
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException("Challenge not found.");

        await VerifyAccessAsync(userId, challenge);

        var prize = await _db.ChallengePrizes
            .Include(p => p.Goal)
            .FirstOrDefaultAsync(p => p.Id == prizeId && p.ChallengeId == challengeId)
            ?? throw new InvalidOperationException("Prize not found.");

        // If prize is linked to an Achievement goal, verify goal is completed
        if (prize.ChallengeGoalId.HasValue && prize.Goal?.Type == "Achievement")
        {
            var goalProgress = await _db.GoalProgresses
                .FirstOrDefaultAsync(p => p.ChallengeGoalId == prize.ChallengeGoalId && p.UserId == userId);
            if (goalProgress == null || !goalProgress.IsCompleted)
                throw new InvalidOperationException("Complete the linked goal before redeeming this prize.");
        }

        // Only deduct cost for challenges with a currency
        if (prize.Cost.HasValue && prize.Cost > 0 && !string.IsNullOrEmpty(challenge.CurrencyName))
        {
            var balance = await _db.ChallengeCurrencyBalances
                .FirstOrDefaultAsync(b => b.ChallengeId == challengeId && b.UserId == userId);

            var currentBalance = balance?.Balance ?? 0;
            if (currentBalance < prize.Cost.Value)
                throw new InvalidOperationException(
                    $"Not enough {challenge.CurrencyName}. You have {currentBalance}, but this prize costs {prize.Cost}.");

            if (balance != null)
            {
                balance.Balance -= prize.Cost.Value;
                balance.UpdatedAt = DateTime.UtcNow;
            }
        }

        var claim = new PrizeClaim
        {
            Id = Guid.NewGuid(),
            ChallengePrizeId = prizeId,
            ChallengeId = challengeId,
            UserId = userId,
            ClaimedAt = DateTime.UtcNow,
        };
        _db.Set<PrizeClaim>().Add(claim);
        await _db.SaveChangesAsync();

        return new PrizeRedeemResponse(
            claim.Id,
            prize.Description,
            prize.Cost ?? 0,
            null,
            claim.ClaimedAt
        );
    }

    public async Task<List<PrizeClaimDto>> GetPrizeClaimsAsync(string userId, Guid challengeId)
    {
        var challenge = await _db.Challenges
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException("Challenge not found.");

        await VerifyAccessAsync(userId, challenge);

        var claims = await _db.Set<PrizeClaim>()
            .Include(c => c.Prize)
            .Include(c => c.User)
            .Where(c => c.ChallengeId == challengeId)
            .OrderByDescending(c => c.ClaimedAt)
            .ToListAsync();

        return claims.Select(c => new PrizeClaimDto(
            c.Id, c.Prize.Description, c.Prize.Cost,
            c.User.Email ?? "unknown",
            c.Notes, c.ClaimedAt
        )).ToList();
    }

    private async Task VerifyAccessAsync(string userId, Challenge challenge)
    {
        if (challenge.Type == "SelfOnly") return;
        var isMember = await _db.FamilyMembers
            .AnyAsync(m => m.FamilyId == challenge.FamilyId && m.UserId == userId);
        if (!isMember)
            throw new UnauthorizedAccessException("You are not a member of this family.");
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null) return "http://localhost:5088";
        return $"{request.Scheme}://{request.Host}";
    }
}
