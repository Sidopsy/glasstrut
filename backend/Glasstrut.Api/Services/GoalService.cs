using Microsoft.EntityFrameworkCore;
using Glasstrut.Api.Data;
using Glasstrut.Api.Models;
using Glasstrut.Api.Models.Dtos;

namespace Glasstrut.Api.Services;

public class GoalService : IGoalService
{
    private readonly AppDbContext _db;

    public GoalService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<LogActivityResponse> LogActivityAsync(string userId, Guid challengeId, Guid activityId, LogProgressRequest request)
    {
        if (request.Amount <= 0)
            throw new InvalidOperationException("Amount must be positive.");

        var activity = await _db.ChallengeActivities
            .Include(a => a.Challenge)
            .Include(a => a.Goal)
                .ThenInclude(g => g!.Prizes)
            .FirstOrDefaultAsync(a => a.Id == activityId && a.ChallengeId == challengeId)
            ?? throw new InvalidOperationException("Activity not found.");

        var challenge = activity.Challenge;

        if (challenge.Type == "SelfOnly")
        {
            if (challenge.CreatedById != userId)
                throw new UnauthorizedAccessException("This challenge does not belong to you.");
        }
        else
        {
            var isMember = await _db.FamilyMembers
                .AnyAsync(m => m.FamilyId == challenge.FamilyId && m.UserId == userId);
            if (!isMember)
                throw new UnauthorizedAccessException("You are not a member of this family.");

            if (challenge.Type == "Targeted")
            {
                var isTargeted = await _db.ChallengeTargets
                    .AnyAsync(t => t.ChallengeId == challengeId && t.UserId == userId);
                if (!isTargeted)
                    throw new UnauthorizedAccessException("You are not targeted in this challenge.");
            }
        }

        decimal? currencyEarned = null;
        if (!string.IsNullOrEmpty(challenge.CurrencyName))
        {
            var raw = request.Amount * activity.PointValue;
            if (raw > 0) currencyEarned = raw;
        }

        var entry = new ProgressEntry
        {
            Id = Guid.NewGuid(),
            ChallengeActivityId = activityId,
            UserId = userId,
            Amount = request.Amount,
            TimeAmount = request.TimeAmount,
            Unit = activity.Unit,
            CurrencyEarned = currencyEarned,
            Notes = request.Notes,
            RecordedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        _db.ProgressEntries.Add(entry);

        // Upsert currency balance BEFORE any early returns (hidden goal path)
        if (currencyEarned.HasValue)
        {
            var today = DateTime.UtcNow.Date;
            var balance = await _db.ChallengeCurrencyBalances
                .FirstOrDefaultAsync(b => b.ChallengeId == challengeId && b.UserId == userId);

            if (balance == null)
            {
                balance = new ChallengeCurrencyBalance
                {
                    Id = Guid.NewGuid(),
                    ChallengeId = challengeId,
                    UserId = userId,
                    Balance = currencyEarned.Value,
                    CurrentStreak = 1,
                    LastActivityDate = today,
                    UpdatedAt = DateTime.UtcNow,
                };
                _db.ChallengeCurrencyBalances.Add(balance);
            }
            else
            {
                balance.Balance += currencyEarned.Value;
                if (balance.LastActivityDate == today)
                {
                    // same day, streak unchanged
                }
                else if (balance.LastActivityDate == today.AddDays(-1))
                {
                    balance.CurrentStreak++;
                }
                else
                {
                    balance.CurrentStreak = 1;
                }
                balance.LastActivityDate = today;
                balance.UpdatedAt = DateTime.UtcNow;
            }
        }

        GoalProgressDto? progressDto = null;

        if (activity.Goal != null)
        {
            var goal = activity.Goal;
            var delta = request.Amount * activity.PointValue;

            var progress = await _db.GoalProgresses
                .FirstOrDefaultAsync(p => p.ChallengeGoalId == goal.Id && p.UserId == userId);

            bool justCompleted = false;
            if (progress == null)
            {
                progress = new GoalProgress
                {
                    Id = Guid.NewGuid(),
                    ChallengeGoalId = goal.Id,
                    UserId = userId,
                    CurrentValue = delta,
                    IsCompleted = goal.TargetValue.HasValue && delta >= goal.TargetValue.Value,
                    CompletedAt = goal.TargetValue.HasValue && delta >= goal.TargetValue.Value
                        ? DateTime.UtcNow : null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                justCompleted = progress.IsCompleted;
                progress.ProgressEntries.Add(entry);
                _db.GoalProgresses.Add(progress);
            }
            else
            {
                progress.CurrentValue += delta;
                progress.ProgressEntries.Add(entry);
                if (goal.TargetValue.HasValue && progress.CurrentValue >= goal.TargetValue.Value && !progress.IsCompleted)
                {
                    progress.IsCompleted = true;
                    progress.CompletedAt = DateTime.UtcNow;
                    justCompleted = true;
                }
                progress.UpdatedAt = DateTime.UtcNow;
            }

            if (justCompleted)
            {
                if (goal.Type == "Achievement")
                    await AutoAwardAchievementAsync(userId, goal);

                if (goal.IsHidden)
                {
                    var linkedPrize = goal.Prizes.FirstOrDefault();
                    if (linkedPrize != null)
                    {
                        await AutoClaimPrizeAsync(userId, challengeId, linkedPrize);
                    }
                    progressDto = MapProgressDto(progress, goal);
                    await _db.SaveChangesAsync();
                    SurpriseDto surprise = linkedPrize != null
                        ? new SurpriseDto(
                            $"Surprise! You earned {linkedPrize.Description}",
                            $"You completed the hidden goal '{goal.Description}' and earned a reward!"
                        )
                        : new SurpriseDto(
                            $"Hidden goal completed: {goal.Description}",
                            "Great job!"
                        );
                    return new LogActivityResponse(progressDto, surprise, currencyEarned);
                }
            }

            progressDto = MapProgressDto(progress, goal);
        }

        await _db.SaveChangesAsync();

        return new LogActivityResponse(progressDto, null, currencyEarned);
    }

    public async Task<ProgressAndAchievementsDto> GetChallengeProgressAsync(string userId, Guid challengeId)
    {
        var challenge = await _db.Challenges
            .Include(c => c.Goals)
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

        var goalIds = challenge.Goals.Select(g => g.Id).ToList();

        var progresses = await _db.GoalProgresses
            .Where(p => goalIds.Contains(p.ChallengeGoalId) && p.UserId == userId)
            .ToListAsync();

        var completedHiddenGoalIds = progresses
            .Where(p => p.IsCompleted)
            .Select(p => p.ChallengeGoalId)
            .ToHashSet();

        var progressDtos = challenge.Goals
            .Where(g => !g.IsHidden || completedHiddenGoalIds.Contains(g.Id))
            .Select(g =>
            {
                var p = progresses.FirstOrDefault(pr => pr.ChallengeGoalId == g.Id);
                return p != null ? MapProgressDto(p, g) : new GoalProgressDto(
                    Guid.Empty, g.Id, g.Description, g.Type,
                    g.TargetValue, g.Unit, 0, false, null);
            }).ToList();

        var achievements = await _db.UserAchievements
            .Include(ua => ua.Achievement)
            .Where(ua => ua.UserId == userId && ua.Achievement.ChallengeId == challengeId)
            .ToListAsync();

        var allChallengeAchievements = await _db.Achievements
            .Where(a => a.ChallengeId == challengeId)
            .ToListAsync();

        var achievementDtos = allChallengeAchievements.Select(a =>
        {
            var ua = achievements.FirstOrDefault(x => x.AchievementId == a.Id);
            return new AchievementDto(
                a.Id, a.Title, a.Description, a.IsHidden,
                a.CreatedAt, ua?.UnlockedAt
            );
        }).Where(a => !a.IsHidden || a.UnlockedAt != null)
          .ToList();

        var currencyBalance = await _db.ChallengeCurrencyBalances
            .FirstOrDefaultAsync(b => b.ChallengeId == challengeId && b.UserId == userId);

        return new ProgressAndAchievementsDto(
            progressDtos, achievementDtos,
            currencyBalance?.Balance ?? 0,
            currencyBalance?.CurrentStreak ?? 0,
            challenge.CurrencyName
        );
    }

    public async Task<ChallengeProgressMembersDto> GetChallengeProgressMembersAsync(string userId, Guid challengeId)
    {
        var challenge = await _db.Challenges
            .Include(c => c.Goals)
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException("Challenge not found.");

        if (challenge.Type != "SelfOnly")
        {
            var isMember = await _db.FamilyMembers
                .AnyAsync(m => m.FamilyId == challenge.FamilyId && m.UserId == userId);
            if (!isMember)
                throw new UnauthorizedAccessException("You are not a member of this family.");
        }

        var goalIds = challenge.Goals.Select(g => g.Id).ToList();

        List<string> memberUserIds;
        List<(string UserId, string Email)> memberInfo;

        if (challenge.Type == "SelfOnly")
        {
            var user = await _db.Users.FindAsync(userId);
            memberUserIds = [userId];
            memberInfo = [(userId, user?.Email ?? "")];
        }
        else
        {
            var members = await _db.FamilyMembers
                .Where(m => m.FamilyId == challenge.FamilyId)
                .Join(_db.Users, m => m.UserId, u => u.Id, (m, u) => new { m.UserId, u.Email })
                .ToListAsync();
            memberUserIds = members.Select(m => m.UserId).ToList();
            memberInfo = members.Select(m => (m.UserId, m.Email ?? "")).ToList();
        }

        var allProgress = await _db.GoalProgresses
            .Where(p => goalIds.Contains(p.ChallengeGoalId) && memberUserIds.Contains(p.UserId))
            .ToListAsync();

        var completedHiddenGoalIds = allProgress
            .Where(p => p.IsCompleted)
            .Select(p => p.ChallengeGoalId)
            .ToHashSet();

        var allBalances = await _db.ChallengeCurrencyBalances
            .Where(b => b.ChallengeId == challengeId && memberUserIds.Contains(b.UserId))
            .ToListAsync();

        var memberDtos = memberInfo.Select(mi =>
        {
            var memberGoals = challenge.Goals
                .Where(g => !g.IsHidden || completedHiddenGoalIds.Contains(g.Id))
                .Select(g =>
                {
                    var p = allProgress.FirstOrDefault(pr => pr.ChallengeGoalId == g.Id && pr.UserId == mi.UserId);
                    return p != null ? MapProgressDto(p, g) : new GoalProgressDto(
                        Guid.Empty, g.Id, g.Description, g.Type,
                        g.TargetValue, g.Unit, 0, false, null);
                }).ToList();

            var b = allBalances.FirstOrDefault(b => b.UserId == mi.UserId);
            return new MemberProgressDto(
                mi.UserId, mi.Email, memberGoals,
                b?.Balance ?? 0,
                b?.CurrentStreak ?? 0,
                challenge.CurrencyName
            );
        }).ToList();

        var allUserAchievements = await _db.UserAchievements
            .Include(ua => ua.Achievement)
            .Where(ua => memberUserIds.Contains(ua.UserId) && ua.Achievement.ChallengeId == challengeId)
            .ToListAsync();

        var allChallengeAchievements = await _db.Achievements
            .Where(a => a.ChallengeId == challengeId)
            .ToListAsync();

        var achievementDtos = allChallengeAchievements.Select(a =>
        {
            var ua = allUserAchievements.FirstOrDefault(x => x.AchievementId == a.Id);
            return new AchievementDto(
                a.Id, a.Title, a.Description, a.IsHidden,
                a.CreatedAt, ua?.UnlockedAt
            );
        }).Where(a => !a.IsHidden || a.UnlockedAt != null)
          .ToList();

        return new ChallengeProgressMembersDto(memberDtos, achievementDtos);
    }

    public async Task<List<ActivityLogEntryDto>> GetActivityLogAsync(string userId, Guid challengeId, int count = 20)
    {
        var challenge = await _db.Challenges
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException("Challenge not found.");

        if (challenge.Type != "SelfOnly")
        {
            var isMember = await _db.FamilyMembers
                .AnyAsync(m => m.FamilyId == challenge.FamilyId && m.UserId == userId);
            if (!isMember)
                throw new UnauthorizedAccessException("You are not a member of this family.");
        }

        var activityLog = await _db.ProgressEntries
            .Include(e => e.Activity)
                .ThenInclude(a => a.Goal)
            .Include(e => e.User)
            .Where(e => e.Activity.ChallengeId == challengeId)
            .OrderByDescending(e => e.RecordedAt)
            .Take(count)
            .ToListAsync();

        return activityLog.Select(e => new ActivityLogEntryDto(
            e.Id,
            e.User.Email ?? "unknown",
            e.Activity.Name,
            e.Activity.Goal?.Description,
            e.Activity.Goal?.Type,
            e.Amount,
            e.TimeAmount,
            e.Unit,
            e.Notes,
            e.RecordedAt,
            e.CurrencyEarned
        )).ToList();
    }

    public async Task<List<AchievementDto>> GetUserAchievementsAsync(string userId)
    {
        var userAchievements = await _db.UserAchievements
            .Include(ua => ua.Achievement)
            .Where(ua => ua.UserId == userId)
            .OrderByDescending(ua => ua.UnlockedAt)
            .ToListAsync();

        return userAchievements.Select(ua => new AchievementDto(
            ua.Achievement.Id,
            ua.Achievement.Title,
            ua.Achievement.Description,
            ua.Achievement.IsHidden,
            ua.Achievement.CreatedAt,
            ua.UnlockedAt
        )).ToList();
    }

    private async Task AutoAwardAchievementAsync(string userId, ChallengeGoal goal)
    {
        var challenge = await _db.Challenges.FindAsync(goal.ChallengeId);

        var achievement = await _db.Achievements
            .FirstOrDefaultAsync(a => a.ChallengeId == goal.ChallengeId && a.ChallengeGoalId == goal.Id);

        if (achievement == null)
        {
            achievement = new Achievement
            {
                Id = Guid.NewGuid(),
                Title = $"Completed: {challenge?.Title ?? "Challenge"} - {goal.Description}",
                Description = goal.IsHidden
                    ? $"Completed the hidden goal '{goal.Description}'!"
                    : $"Completed a goal in the challenge.",
                IsHidden = goal.IsHidden,
                ChallengeId = goal.ChallengeId,
                ChallengeGoalId = goal.Id,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Achievements.Add(achievement);
        }

        var alreadyAwarded = await _db.UserAchievements
            .AnyAsync(ua => ua.AchievementId == achievement.Id && ua.UserId == userId);
        if (alreadyAwarded)
            return;

        var userAchievement = new UserAchievement
        {
            Id = Guid.NewGuid(),
            AchievementId = achievement.Id,
            UserId = userId,
            UnlockedAt = DateTime.UtcNow,
        };
        _db.UserAchievements.Add(userAchievement);
    }

    private async Task AutoClaimPrizeAsync(string userId, Guid challengeId, ChallengePrize prize)
    {
        var alreadyClaimed = await _db.Set<PrizeClaim>()
            .AnyAsync(c => c.ChallengePrizeId == prize.Id && c.UserId == userId);
        if (alreadyClaimed)
            return;

        var claim = new PrizeClaim
        {
            Id = Guid.NewGuid(),
            ChallengePrizeId = prize.Id,
            ChallengeId = challengeId,
            UserId = userId,
            Notes = "Auto-awarded from hidden goal completion",
            ClaimedAt = DateTime.UtcNow,
        };
        _db.Set<PrizeClaim>().Add(claim);
    }

    private static GoalProgressDto MapProgressDto(GoalProgress p, ChallengeGoal g)
    {
        return new GoalProgressDto(
            p.Id, g.Id, g.Description, g.Type,
            g.TargetValue, g.Unit,
            p.CurrentValue, p.IsCompleted, p.CompletedAt
        );
    }
}
