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
        var allowedChallengeTypes = new[] { "SelfOnly", "FamilyWide", "Targeted" };
        if (!allowedChallengeTypes.Contains(request.Type))
            throw new InvalidOperationException($"Invalid challenge type: {request.Type}.");

        if (request.Type != "SelfOnly" && request.FamilyId == null)
            throw new InvalidOperationException("Family challenges must specify a family.");

        if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate <= request.StartDate)
            throw new InvalidOperationException("End date must be after start date.");

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
        AddChallengeActivities(challenge, request.Activities);

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
            .Include(c => c.Activities)
                .ThenInclude(a => a.GoalLinks)
            .Include(c => c.Prizes)
            .Include(c => c.Targets)
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException("Challenge not found.");

        if (challenge.CreatedById != userId)
            throw new UnauthorizedAccessException("Only the creator can edit this challenge.");

        if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate <= request.StartDate)
            throw new InvalidOperationException("End date must be after start date.");

        challenge.Title = request.Title;
        challenge.Description = request.Description;
        challenge.StartDate = request.StartDate;
        challenge.EndDate = request.EndDate;
        challenge.CurrencyName = request.CurrencyName;

        await MergeGoalsAndPrizesAsync(challenge, request.Goals, request.Prizes);
        await MergeChallengeActivitiesAsync(challenge, request.Activities);
        await _db.SaveChangesAsync();

        return await GetChallengeAsync(userId, challenge.Id);
    }

    public async Task DeleteChallengeAsync(string userId, Guid challengeId)
    {
        var challenge = await _db.Challenges
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException("Challenge not found.");

        if (challenge.CreatedById != userId)
            throw new UnauthorizedAccessException("Only the creator can delete this challenge.");

        var activityIds = await _db.ChallengeActivities
            .Where(a => a.ChallengeId == challengeId)
            .Select(a => a.Id)
            .ToListAsync();

        var goalIds = await _db.ChallengeGoals
            .Where(g => g.ChallengeId == challengeId)
            .Select(g => g.Id)
            .ToListAsync();

        var achievementIds = await _db.Achievements
            .Where(a => a.ChallengeId == challengeId)
            .Select(a => a.Id)
            .ToListAsync();

        await _db.ProgressEntries.Where(e => activityIds.Contains(e.ChallengeActivityId)).ExecuteDeleteAsync();
        await _db.GoalProgresses.Where(p => goalIds.Contains(p.ChallengeGoalId)).ExecuteDeleteAsync();
        await _db.UserAchievements.Where(ua => achievementIds.Contains(ua.AchievementId)).ExecuteDeleteAsync();
        await _db.Achievements.Where(a => a.ChallengeId == challengeId).ExecuteDeleteAsync();
        await _db.Set<PrizeClaim>().Where(c => c.ChallengeId == challengeId).ExecuteDeleteAsync();
        await _db.ChallengeCurrencyBalances.Where(b => b.ChallengeId == challengeId).ExecuteDeleteAsync();
        await _db.ChallengeActivities.Where(a => a.ChallengeId == challengeId).ExecuteDeleteAsync();
        await _db.ChallengeGoals.Where(g => g.ChallengeId == challengeId).ExecuteDeleteAsync();
        await _db.ChallengePrizes.Where(p => p.ChallengeId == challengeId).ExecuteDeleteAsync();
        await _db.ChallengeTargets.Where(t => t.ChallengeId == challengeId).ExecuteDeleteAsync();
        _db.Challenges.Remove(challenge);
        await _db.SaveChangesAsync();
    }

    private async Task MergeChallengeActivitiesAsync(Challenge challenge, List<UpdateActivityDto>? activityDtos)
    {
        if (activityDtos == null) return;

        var activityIdsWithProgress = await _db.ProgressEntries
            .Where(e => e.Activity.ChallengeId == challenge.Id)
            .Select(e => e.ChallengeActivityId)
            .Distinct()
            .ToListAsync();

        var requestActivityIds = activityDtos.Where(a => a.Id.HasValue).Select(a => a.Id!.Value).ToHashSet();

        var activitiesToRemove = challenge.Activities
            .Where(a => !activityIdsWithProgress.Contains(a.Id) && !requestActivityIds.Contains(a.Id))
            .ToList();
        foreach (var a in activitiesToRemove)
            challenge.Activities.Remove(a);

        var validGoalIds = challenge.Goals.Select(g => g.Id).ToHashSet();

        foreach (var ad in activityDtos)
        {
            var existing = ad.Id.HasValue ? challenge.Activities.FirstOrDefault(a => a.Id == ad.Id.Value) : null;
            if (existing != null)
            {
                existing.Name = ad.Name;
                existing.ActivityType = ad.ActivityType;
                existing.Unit = ad.Unit;
                existing.TimeUnit = ad.TimeUnit;
                existing.PointValue = ad.PointValue;

                if (ad.GoalIds != null)
                {
                    var invalidGoalIds = ad.GoalIds.Except(validGoalIds).ToList();
                    if (invalidGoalIds.Count != 0)
                        throw new InvalidOperationException($"Activity links to goals not in this challenge: {string.Join(", ", invalidGoalIds)}");

                    var currentGoalIds = existing.GoalLinks.Select(gl => gl.ChallengeGoalId).ToHashSet();
                    var requestedGoalIds = ad.GoalIds.ToHashSet();

                    var linksToRemove = existing.GoalLinks
                        .Where(gl => !requestedGoalIds.Contains(gl.ChallengeGoalId))
                        .ToList();
                    foreach (var lr in linksToRemove)
                        existing.GoalLinks.Remove(lr);

                    foreach (var gid in requestedGoalIds)
                    {
                        if (!currentGoalIds.Contains(gid))
                        {
                            existing.GoalLinks.Add(new ChallengeActivityGoal
                            {
                                ChallengeActivityId = existing.Id,
                                ChallengeGoalId = gid
                            });
                        }
                    }
                }
            }
            else
            {
                var activity = new ChallengeActivity
                {
                    Id = Guid.NewGuid(),
                    ChallengeId = challenge.Id,
                    Name = ad.Name,
                    ActivityType = ad.ActivityType,
                    Unit = ad.Unit,
                    TimeUnit = ad.TimeUnit,
                    PointValue = ad.PointValue,
                    CreatedAt = DateTime.UtcNow,
                };

                if (ad.GoalIds != null)
                {
                    var invalidGoalIds = ad.GoalIds.Except(validGoalIds).ToList();
                    if (invalidGoalIds.Count != 0)
                        throw new InvalidOperationException($"Activity links to goals not in this challenge: {string.Join(", ", invalidGoalIds)}");

                    foreach (var gid in ad.GoalIds)
                    {
                        activity.GoalLinks.Add(new ChallengeActivityGoal
                        {
                            ChallengeActivityId = activity.Id,
                            ChallengeGoalId = gid
                        });
                    }
                }

                challenge.Activities.Add(activity);
            }
        }
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

        if (goalDtos != null)
        {
            var requestGoalIds = goalDtos.Where(g => g.Id.HasValue).Select(g => g.Id!.Value).ToHashSet();
            var goalsToRemove = challenge.Goals
                .Where(g => !goalIdsWithProgress.Contains(g.Id) && !requestGoalIds.Contains(g.Id))
                .ToList();
            foreach (var g in goalsToRemove) challenge.Goals.Remove(g);
        }

        if (prizeDtos != null)
        {
            var requestPrizeIds = prizeDtos.Where(p => p.Id.HasValue).Select(p => p.Id!.Value).ToHashSet();
            var prizesToRemove = challenge.Prizes
                .Where(p => !prizeIdsWithClaims.Contains(p.Id) && !requestPrizeIds.Contains(p.Id))
                .ToList();
            foreach (var p in prizesToRemove) challenge.Prizes.Remove(p);
        }

        if (goalDtos != null)
        {
            foreach (var gd in goalDtos)
            {
                var existing = gd.Id.HasValue ? challenge.Goals.FirstOrDefault(g => g.Id == gd.Id.Value) : null;
                if (existing != null)
                {
                    existing.Description = gd.Description;
                    existing.Type = gd.Type;
                    existing.TargetValue = gd.Type is "Achievement" or "Streak" ? gd.TargetValue : null;
                    existing.Unit = gd.Unit;
                    existing.IsHidden = gd.IsHidden;
                    existing.IsPerEntry = gd.IsPerEntry;
                }
                else
                {
                    challenge.Goals.Add(new ChallengeGoal
                    {
                        Id = Guid.NewGuid(),
                        ChallengeId = challenge.Id,
                        Description = gd.Description,
                        Type = gd.Type,
                        TargetValue = gd.Type is "Achievement" or "Streak" ? gd.TargetValue : null,
                        Unit = gd.Unit,
                        IsHidden = gd.IsHidden,
                        IsPerEntry = gd.IsPerEntry,
                        CreatedAt = DateTime.UtcNow,
                    });
                }
            }
        }

        if (prizeDtos != null)
        {
            var validGoalIds = challenge.Goals.Select(g => g.Id).ToHashSet();
            foreach (var pd in prizeDtos)
            {
                if (pd.ChallengeGoalId.HasValue && !validGoalIds.Contains(pd.ChallengeGoalId.Value))
                    throw new InvalidOperationException("Prize links to a goal that does not belong to this challenge.");

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
            .Include(c => c.Activities)
                .ThenInclude(a => a.GoalLinks)
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

        var hiddenGoalIds = challenge.Goals.Where(g => g.IsHidden).Select(g => g.Id).ToHashSet();
        HashSet<Guid> completedHidden = [];
        if (hiddenGoalIds.Count > 0)
        {
            var completedIds = await _db.GoalProgresses
                .Where(p => hiddenGoalIds.Contains(p.ChallengeGoalId) && p.UserId == userId && p.IsCompleted)
                .Select(p => p.ChallengeGoalId)
                .ToListAsync();
            completedHidden = completedIds.ToHashSet();
        }

        return MapToDto(challenge, completedHidden);
    }

    public async Task<List<ChallengeDto>> GetChallengesAsync(string userId, Guid? familyId)
    {
        var query = _db.Challenges
            .Include(c => c.Goals)
            .Include(c => c.Activities)
                .ThenInclude(a => a.GoalLinks)
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

        var challenges = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();

        var allHiddenGoalIds = challenges
            .SelectMany(c => c.Goals.Where(g => g.IsHidden).Select(g => g.Id))
            .ToHashSet();
        Dictionary<Guid, HashSet<Guid>> completedByChallenge = [];
        if (allHiddenGoalIds.Count > 0)
        {
            var completedProgresses = await _db.GoalProgresses
                .Where(p => allHiddenGoalIds.Contains(p.ChallengeGoalId) && p.UserId == userId && p.IsCompleted)
                .ToListAsync();
            foreach (var cp in completedProgresses)
                completedByChallenge[cp.ChallengeGoalId] = [];
            foreach (var cp in completedProgresses)
                completedByChallenge[cp.ChallengeGoalId].Add(cp.ChallengeGoalId);
        }

        return challenges.Select(c =>
        {
            var hiddenIds = c.Goals.Where(g => g.IsHidden).Select(g => g.Id).ToHashSet();
            var completed = hiddenIds
                .Where(h => completedByChallenge.TryGetValue(h, out var set) && set.Contains(h))
                .ToHashSet();
            return MapToDto(c, completed);
        }).ToList();
    }

    private static void AddChallengeActivities(Challenge challenge, List<CreateActivityDto>? activityDtos)
    {
        if (activityDtos == null) return;

        var validGoalIds = challenge.Goals.Select(g => g.Id).ToHashSet();

        foreach (var ad in activityDtos)
        {
            var activity = new ChallengeActivity
            {
                Id = Guid.NewGuid(),
                ChallengeId = challenge.Id,
                Name = ad.Name,
                ActivityType = ad.ActivityType,
                Unit = ad.Unit,
                TimeUnit = ad.TimeUnit,
                PointValue = ad.PointValue,
                CreatedAt = DateTime.UtcNow,
            };

            if (ad.GoalIndices != null)
            {
                var selectedGoals = ad.GoalIndices
                    .Select(i =>
                    {
                        if (i < 0 || i >= challenge.Goals.Count)
                            throw new InvalidOperationException($"Goal index {i} is out of range. Challenge has {challenge.Goals.Count} goals.");
                        return challenge.Goals.ElementAt(i);
                    })
                    .ToList();

                foreach (var goal in selectedGoals)
                {
                    activity.GoalLinks.Add(new ChallengeActivityGoal
                    {
                        ChallengeActivityId = activity.Id,
                        ChallengeGoalId = goal.Id
                    });
                }
            }

            challenge.Activities.Add(activity);
        }
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
                    TargetValue = goalDto.Type is "Achievement" or "Streak" ? goalDto.TargetValue : null,
                    Unit = goalDto.Unit,
                    IsHidden = goalDto.IsHidden,
                    IsPerEntry = goalDto.IsPerEntry,
                    CreatedAt = DateTime.UtcNow,
                };

                challenge.Goals.Add(goal);
            }
        }

        if (prizeDtos != null)
        {
            var validGoalIds = challenge.Goals.Select(g => g.Id).ToHashSet();
            foreach (var prizeDto in prizeDtos)
            {
                if (prizeDto.ChallengeGoalId.HasValue && !validGoalIds.Contains(prizeDto.ChallengeGoalId.Value))
                    throw new InvalidOperationException("Prize links to a goal that does not belong to this challenge.");

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

    private static ChallengeDto MapToDto(Challenge c, HashSet<Guid>? completedHiddenGoalIds = null)
    {
        return new ChallengeDto(
            c.Id, c.Title, c.Description, c.Type, c.FamilyId,
            c.StartDate, c.EndDate, c.CreatedAt, c.CurrencyName, c.CreatedById,
            c.Goals.Select(g => new ChallengeGoalDto(
                g.Id, g.Description, g.Type, g.TargetValue, g.Unit, g.IsHidden, g.IsPerEntry
            )).ToList(),
            c.Prizes
                .Where(p => p.ChallengeGoalId == null
                    || !c.Goals.Any(g => g.Id == p.ChallengeGoalId && g.IsHidden)
                    || (completedHiddenGoalIds?.Contains(p.ChallengeGoalId.Value) ?? false))
                .Select(p => new ChallengePrizeDto(
                    p.Id, p.Description, p.Cost, p.HasQR, p.ChallengeGoalId
                )).ToList(),
            c.Targets.Select(t => t.UserId).ToList(),
            c.Activities.Select(a => new ChallengeActivityDto(
                a.Id, a.Name, a.ActivityType, a.Unit, a.TimeUnit, a.PointValue,
                a.GoalLinks.Select(gl => gl.ChallengeGoalId).ToList()
            )).ToList()
        );
    }
}
