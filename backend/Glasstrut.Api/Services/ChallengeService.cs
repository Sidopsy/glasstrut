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

        AddGoalsAndPrizes(challenge, request.Goals, request.Prizes);

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

    public async Task<ChallengeDto> UpdateChallengeAsync(string userId, Guid challengeId, UpdateChallengeRequest request)
    {
        var challenge = await _db.Challenges
            .Include(c => c.Goals)
                .ThenInclude(g => g.Activities)
            .Include(c => c.Prizes)
            .Include(c => c.Targets)
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException("Challenge not found.");

        if (challenge.CreatedById != userId)
            throw new UnauthorizedAccessException("Only the creator can edit this challenge.");

        challenge.Title = request.Title;
        challenge.Description = request.Description;
        challenge.StartDate = request.StartDate;
        challenge.EndDate = request.EndDate;
        challenge.CurrencyName = request.CurrencyName;

        await MergeGoalsAndPrizesAsync(challenge, request.Goals, request.Prizes);
        await _db.SaveChangesAsync();

        return await GetChallengeAsync(userId, challenge.Id);
    }

    private async Task MergeGoalsAndPrizesAsync(Challenge challenge, List<UpdateGoalDto>? goalDtos, List<UpdatePrizeDto>? prizeDtos)
    {
        var goalIdsWithProgress = await _db.GoalProgresses
            .Where(p => p.Goal.ChallengeId == challenge.Id)
            .Select(p => p.ChallengeGoalId)
            .Distinct()
            .ToListAsync();

        var prizeIdsWithClaims = await _db.Set<PrizeClaim>()
            .Where(c => c.ChallengeId == challenge.Id)
            .Select(c => c.ChallengePrizeId)
            .Distinct()
            .ToListAsync();

        var requestGoalIds = goalDtos?.Where(g => g.Id.HasValue).Select(g => g.Id!.Value).ToHashSet() ?? [];
        var requestPrizeIds = prizeDtos?.Where(p => p.Id.HasValue).Select(p => p.Id!.Value).ToHashSet() ?? [];

        var goalsToRemove = challenge.Goals
            .Where(g => !goalIdsWithProgress.Contains(g.Id) && !requestGoalIds.Contains(g.Id))
            .ToList();
        foreach (var g in goalsToRemove) challenge.Goals.Remove(g);

        var prizesToRemove = challenge.Prizes
            .Where(p => !prizeIdsWithClaims.Contains(p.Id) && !requestPrizeIds.Contains(p.Id))
            .ToList();
        foreach (var p in prizesToRemove) challenge.Prizes.Remove(p);

        if (goalDtos != null)
        {
            foreach (var gd in goalDtos)
            {
                var existing = gd.Id.HasValue ? challenge.Goals.FirstOrDefault(g => g.Id == gd.Id.Value) : null;
                if (existing != null)
                {
                    existing.Description = gd.Description;
                    existing.Type = gd.Type;
                    existing.TargetValue = gd.Type == "Achievement" ? gd.TargetValue : null;
                    existing.Unit = gd.Unit;
                    existing.IsHidden = gd.IsHidden;

                    var activityIdsWithProgress = await _db.ProgressEntries
                        .Where(e => e.Activity.Goal.Id == existing.Id)
                        .Select(e => e.ChallengeActivityId)
                        .Distinct()
                        .ToListAsync();

                    var requestActivityIds = gd.Activities?.Where(a => a.Id.HasValue).Select(a => a.Id!.Value).ToHashSet() ?? [];
                    var activitiesToRemove = existing.Activities
                        .Where(a => !activityIdsWithProgress.Contains(a.Id) && !requestActivityIds.Contains(a.Id))
                        .ToList();
                    foreach (var a in activitiesToRemove) existing.Activities.Remove(a);

                    if (gd.Activities != null)
                    {
                        foreach (var ad in gd.Activities)
                        {
                            var existingAct = ad.Id.HasValue ? existing.Activities.FirstOrDefault(a => a.Id == ad.Id.Value) : null;
                            if (existingAct != null)
                            {
                                existingAct.Name = ad.Name;
                                existingAct.ActivityType = ad.ActivityType;
                                existingAct.Unit = ad.Unit;
                                existingAct.PointValue = ad.PointValue;
                            }
                            else
                            {
                                existing.Activities.Add(new ChallengeActivity
                                {
                                    Id = Guid.NewGuid(),
                                    ChallengeId = challenge.Id,
                                    ChallengeGoalId = existing.Id,
                                    Name = ad.Name,
                                    ActivityType = ad.ActivityType,
                                    Unit = ad.Unit,
                                    PointValue = ad.PointValue,
                                    CreatedAt = DateTime.UtcNow,
                                });
                            }
                        }
                    }
                }
                else
                {
                    var newGoal = new ChallengeGoal
                    {
                        Id = Guid.NewGuid(),
                        ChallengeId = challenge.Id,
                        Description = gd.Description,
                        Type = gd.Type,
                        TargetValue = gd.Type == "Achievement" ? gd.TargetValue : null,
                        Unit = gd.Unit,
                        IsHidden = gd.IsHidden,
                        CreatedAt = DateTime.UtcNow,
                    };
                    if (gd.Activities != null)
                    {
                        foreach (var ad in gd.Activities)
                        {
                            newGoal.Activities.Add(new ChallengeActivity
                            {
                                Id = Guid.NewGuid(),
                                ChallengeId = challenge.Id,
                                ChallengeGoalId = newGoal.Id,
                                Name = ad.Name,
                                ActivityType = ad.ActivityType,
                                Unit = ad.Unit,
                                PointValue = ad.PointValue,
                                CreatedAt = DateTime.UtcNow,
                            });
                        }
                    }
                    challenge.Goals.Add(newGoal);
                }
            }
        }

        if (prizeDtos != null)
        {
            foreach (var pd in prizeDtos)
            {
                var existing = pd.Id.HasValue ? challenge.Prizes.FirstOrDefault(p => p.Id == pd.Id.Value) : null;
                if (existing != null)
                {
                    existing.Description = pd.Description;
                    existing.Cost = pd.Cost;
                    existing.HasQR = pd.HasQR;
                    existing.ChallengeGoalId = pd.ChallengeGoalId;
                }
                else
                {
                    challenge.Prizes.Add(new ChallengePrize
                    {
                        Id = Guid.NewGuid(),
                        ChallengeId = challenge.Id,
                        Description = pd.Description,
                        Cost = pd.Cost,
                        HasQR = pd.HasQR,
                        ChallengeGoalId = pd.ChallengeGoalId,
                        CreatedAt = DateTime.UtcNow,
                    });
                }
            }
        }
    }

    public async Task<ChallengeDto> GetChallengeAsync(string userId, Guid challengeId)
    {
        var challenge = await _db.Challenges
            .Include(c => c.Goals)
                .ThenInclude(g => g.Activities)
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
                .ThenInclude(g => g.Activities)
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
            var userFamilyIds = await _db.FamilyMembers
                .Where(m => m.UserId == userId)
                .Select(m => m.FamilyId)
                .ToListAsync();

            query = query.Where(c =>
                (c.Type == "SelfOnly" && c.CreatedById == userId) ||
                (c.Type != "SelfOnly" && c.FamilyId != null && userFamilyIds.Contains(c.FamilyId.Value)));
        }

        return await query.OrderByDescending(c => c.CreatedAt)
                          .Select(c => MapToDto(c))
                          .ToListAsync();
    }

    private static void AddGoalsAndPrizes(Challenge challenge, List<CreateGoalDto>? goalDtos, List<CreatePrizeDto>? prizeDtos)
    {
        if (goalDtos != null)
        {
            foreach (var goalDto in goalDtos)
            {
                var goal = new ChallengeGoal
                {
                    Id = Guid.NewGuid(),
                    ChallengeId = challenge.Id,
                    Description = goalDto.Description,
                    Type = goalDto.Type,
                    TargetValue = goalDto.Type == "Achievement" ? goalDto.TargetValue : null,
                    Unit = goalDto.Unit,
                    IsHidden = goalDto.IsHidden,
                    CreatedAt = DateTime.UtcNow,
                };

                if (goalDto.Activities != null)
                {
                    foreach (var activityDto in goalDto.Activities)
                    {
                        var activity = new ChallengeActivity
                        {
                            Id = Guid.NewGuid(),
                            ChallengeId = challenge.Id,
                            ChallengeGoalId = goal.Id,
                            Name = activityDto.Name,
                            ActivityType = activityDto.ActivityType,
                            Unit = activityDto.Unit,
                            PointValue = activityDto.PointValue,
                            CreatedAt = DateTime.UtcNow,
                        };
                        goal.Activities.Add(activity);
                        challenge.Activities.Add(activity);
                    }
                }

                challenge.Goals.Add(goal);
            }
        }

        if (prizeDtos != null)
        {
            foreach (var prizeDto in prizeDtos)
            {
                var prize = new ChallengePrize
                {
                    Id = Guid.NewGuid(),
                    ChallengeId = challenge.Id,
                    Description = prizeDto.Description,
                    Cost = prizeDto.Cost,
                    HasQR = prizeDto.HasQR,
                    ChallengeGoalId = prizeDto.ChallengeGoalId,
                    CreatedAt = DateTime.UtcNow,
                };
                challenge.Prizes.Add(prize);
            }
        }
    }

    private static ChallengeDto MapToDto(Challenge c)
    {
        return new ChallengeDto(
            c.Id, c.Title, c.Description, c.Type, c.FamilyId,
            c.StartDate, c.EndDate, c.CreatedAt, c.CurrencyName, c.CreatedById,
            c.Goals.Select(g => new ChallengeGoalDto(
                g.Id, g.Description, g.Type, g.TargetValue, g.Unit, g.IsHidden,
                g.Activities.Select(a => new ChallengeActivityDto(
                    a.Id, a.Name, a.ActivityType, a.Unit, a.PointValue
                )).ToList()
            )).ToList(),
            c.Prizes.Select(p => new ChallengePrizeDto(
                p.Id, p.Description, p.Cost, p.HasQR, p.ChallengeGoalId
            )).ToList(),
            c.Targets.Select(t => t.UserId).ToList()
        );
    }
}
