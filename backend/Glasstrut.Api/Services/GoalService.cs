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

    public async Task<GoalProgressDto> RecordProgressAsync(string userId, Guid challengeGoalId, RecordProgressRequest request)
    {
        var goal = await _db.ChallengeGoals
            .Include(g => g.Challenge)
            .FirstOrDefaultAsync(g => g.Id == challengeGoalId)
            ?? throw new InvalidOperationException("Goal not found.");

        var challenge = goal.Challenge;

        if (challenge.Type != "SelfOnly")
        {
            var isMember = await _db.FamilyMembers
                .AnyAsync(m => m.FamilyId == challenge.FamilyId && m.UserId == userId);
            if (!isMember)
                throw new UnauthorizedAccessException("You are not a member of this family.");
        }

        var progress = await _db.GoalProgresses
            .FirstOrDefaultAsync(p => p.ChallengeGoalId == challengeGoalId && p.UserId == userId);

        if (progress == null)
        {
            progress = new GoalProgress
            {
                Id = Guid.NewGuid(),
                ChallengeGoalId = challengeGoalId,
                UserId = userId,
                CurrentValue = request.CurrentValue,
                IsCompleted = goal.TargetValue.HasValue && request.CurrentValue >= goal.TargetValue.Value,
                CompletedAt = goal.TargetValue.HasValue && request.CurrentValue >= goal.TargetValue.Value
                    ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.GoalProgresses.Add(progress);
        }
        else
        {
            progress.CurrentValue = request.CurrentValue;
            if (goal.TargetValue.HasValue && request.CurrentValue >= goal.TargetValue.Value && !progress.IsCompleted)
            {
                progress.IsCompleted = true;
                progress.CompletedAt = DateTime.UtcNow;
            }
            progress.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        if (progress.IsCompleted)
        {
            await AutoAwardAchievementAsync(userId, goal);
        }

        return MapProgressDto(progress, goal);
    }

    public async Task<ProgressAndAchievementsDto> GetChallengeProgressAsync(string userId, Guid challengeId)
    {
        var challenge = await _db.Challenges
            .Include(c => c.Goals)
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException("Challenge not found.");

        var goalIds = challenge.Goals.Select(g => g.Id).ToList();

        var progresses = await _db.GoalProgresses
            .Where(p => goalIds.Contains(p.ChallengeGoalId) && p.UserId == userId)
            .ToListAsync();

        var progressDtos = challenge.Goals.Select(g =>
        {
            var p = progresses.FirstOrDefault(pr => pr.ChallengeGoalId == g.Id);
            return p != null ? MapProgressDto(p, g) : new GoalProgressDto(
                Guid.Empty, g.Id, g.Description,
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

        return new ProgressAndAchievementsDto(progressDtos, achievementDtos);
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
        var alreadyAwarded = await _db.UserAchievements
            .AnyAsync(ua => ua.UserId == userId && ua.Achievement.ChallengeId == goal.ChallengeId);

        if (alreadyAwarded)
            return;

        var achievement = await _db.Achievements
            .FirstOrDefaultAsync(a => a.ChallengeId == goal.ChallengeId);

        if (achievement == null)
        {
            var challenge = await _db.Challenges.FindAsync(goal.ChallengeId);
            achievement = new Achievement
            {
                Id = Guid.NewGuid(),
                Title = $"Completed: {challenge?.Title ?? "Challenge"}",
                Description = $"Completed a goal in the challenge.",
                IsHidden = false,
                ChallengeId = goal.ChallengeId,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Achievements.Add(achievement);
        }

        var userAchievement = new UserAchievement
        {
            Id = Guid.NewGuid(),
            AchievementId = achievement.Id,
            UserId = userId,
            UnlockedAt = DateTime.UtcNow,
        };
        _db.UserAchievements.Add(userAchievement);
        await _db.SaveChangesAsync();
    }

    private static GoalProgressDto MapProgressDto(GoalProgress p, ChallengeGoal g)
    {
        return new GoalProgressDto(
            p.Id, g.Id, g.Description,
            g.TargetValue, g.Unit,
            p.CurrentValue, p.IsCompleted, p.CompletedAt
        );
    }
}
