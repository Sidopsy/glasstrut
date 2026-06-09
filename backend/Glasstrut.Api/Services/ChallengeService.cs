using Microsoft.EntityFrameworkCore;
using Glasstrut.Api.Data;
using Glasstrut.Api.Models;
using Glasstrut.Api.Models.Dtos;

namespace Glasstrut.Api.Services;

public class ChallengeService : IChallengeService
{
    private readonly AppDbContext _db;

    public ChallengeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ChallengeDto> CreateChallengeAsync(string userId, CreateChallengeRequest request)
    {
        if (request.Type != "SelfOnly" && request.FamilyId == null)
            throw new InvalidOperationException("Family challenges must specify a family.");

        if (request.Type == "FamilyWide" || request.Type == "Targeted")
        {
            var isMember = await _db.FamilyMembers
                .AnyAsync(m => m.FamilyId == request.FamilyId && m.UserId == userId);
            if (!isMember)
                throw new UnauthorizedAccessException("You are not a member of this family.");
        }

        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Type = request.Type,
            FamilyId = request.Type == "SelfOnly" ? null : request.FamilyId,
            CreatedById = userId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CurrencyName = request.CurrencyName,
            CreatedAt = DateTime.UtcNow,
        };

        if (request.Goals != null)
        {
            foreach (var goalDto in request.Goals)
            {
                challenge.Goals.Add(new ChallengeGoal
                {
                    Id = Guid.NewGuid(),
                    ChallengeId = challenge.Id,
                    Description = goalDto.Description,
                    TargetValue = goalDto.TargetValue,
                    Unit = goalDto.Unit,
                    PointValue = goalDto.PointValue,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        if (request.Prizes != null)
        {
            foreach (var prizeDto in request.Prizes)
            {
                challenge.Prizes.Add(new ChallengePrize
                {
                    Id = Guid.NewGuid(),
                    ChallengeId = challenge.Id,
                    Description = prizeDto.Description,
                    Cost = prizeDto.Cost,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        if (request.Type == "Targeted" && request.TargetUserIds != null)
        {
            var validTargets = await _db.FamilyMembers
                .Where(m => m.FamilyId == request.FamilyId && request.TargetUserIds.Contains(m.UserId))
                .Select(m => m.UserId)
                .ToListAsync();

            var invalid = request.TargetUserIds.Except(validTargets).ToList();
            if (invalid.Count != 0)
                throw new InvalidOperationException(
                    $"Users are not members of this family: {string.Join(", ", invalid)}");

            foreach (var targetUserId in validTargets)
            {
                challenge.Targets.Add(new ChallengeTarget
                {
                    Id = Guid.NewGuid(),
                    ChallengeId = challenge.Id,
                    UserId = targetUserId,
                });
            }
        }

        _db.Challenges.Add(challenge);
        await _db.SaveChangesAsync();

        return await GetChallengeAsync(userId, challenge.Id);
    }

    public async Task<ChallengeDto> GetChallengeAsync(string userId, Guid challengeId)
    {
        var challenge = await _db.Challenges
            .Include(c => c.Goals)
            .Include(c => c.Prizes)
            .Include(c => c.Targets)
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException("Challenge not found.");

        if (challenge.Type != "SelfOnly")
        {
            var isMember = await _db.FamilyMembers
                .AnyAsync(m => m.FamilyId == challenge.FamilyId && m.UserId == userId);
            if (!isMember)
                throw new UnauthorizedAccessException("You are not a member of this family.");
        }
        else if (challenge.CreatedById != userId)
        {
            throw new UnauthorizedAccessException("This challenge does not belong to you.");
        }

        return MapToDto(challenge);
    }

    public async Task<List<ChallengeDto>> GetChallengesAsync(string userId, Guid? familyId)
    {
        var query = _db.Challenges
            .Include(c => c.Goals)
            .Include(c => c.Prizes)
            .Include(c => c.Targets)
            .AsQueryable();

        if (familyId.HasValue)
        {
            var isMember = await _db.FamilyMembers
                .AnyAsync(m => m.FamilyId == familyId && m.UserId == userId);
            if (!isMember)
                throw new UnauthorizedAccessException("You are not a member of this family.");

            query = query.Where(c => c.FamilyId == familyId);
        }
        else
        {
            query = query.Where(c => c.CreatedById == userId && c.Type == "SelfOnly");
        }

        return await query.OrderByDescending(c => c.CreatedAt)
                          .Select(c => MapToDto(c))
                          .ToListAsync();
    }

    private static ChallengeDto MapToDto(Challenge c)
    {
        return new ChallengeDto(
            c.Id, c.Title, c.Description, c.Type, c.FamilyId,
            c.StartDate, c.EndDate, c.CreatedAt, c.CurrencyName,
            c.Goals.Select(g => new ChallengeGoalDto(g.Id, g.Description, g.TargetValue, g.Unit, g.PointValue)).ToList(),
            c.Prizes.Select(p => new ChallengePrizeDto(p.Id, p.Description, p.Cost)).ToList(),
            c.Targets.Select(t => t.UserId).ToList()
        );
    }
}
