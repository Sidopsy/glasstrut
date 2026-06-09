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

    public async Task<GoalProgressDto> LogActivityAsync(string userId, Guid challengeId, Guid activityId, LogProgressRequest request)
    {
        var activity = await _db.ChallengeActivities
            .Include(a => a.Goal)
                .ThenInclude(g => g.Challenge)
            .FirstOrDefaultAsync(a => a.Id == activityId && a.ChallengeId == challengeId)
            ?? throw new InvalidOperationException("Activity not found.");

        var challenge = activity.Goal.Challenge;

        if (challenge.Type != "SelfOnly")
        {
            var isMember = await _db.FamilyMembers
                .AnyAsync(m => m.FamilyId == challenge.FamilyId && m.UserId == userId);
            if (!isMember)
                throw new UnauthorizedAccessException("You are not a member of this family.");
        }

        var goal = activity.Goal;

        var entry = new ProgressEntry
        {
            Id = Guid.NewGuid(),
            ChallengeActivityId = activityId,
            UserId = userId,
            Amount = request.Amount,
            Unit = activity.Unit,
            Notes = request.Notes,
            RecordedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        _db.ProgressEntries.Add(entry);

        var delta = request.Amount * activity.PointValue;

        var progress = await _db.GoalProgresses
            .FirstOrDefaultAsync(p => p.ChallengeGoalId == goal.Id && p.UserId == userId);

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
            progress.ProgressEntries.Add(entry);
            _db.GoalProgresses.Add(progress);
        }
        else
        {
            progress.CurrentValue += delta;
            if (goal.TargetValue.HasValue && progress.CurrentValue >= goal.TargetValue.Value && !progress.IsCompleted)
            {
                progress.IsCompleted = true;
                progress.CompletedAt = DateTime.UtcNow;
            }
            progress.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        if (progress.IsCompleted && goal.Type == "Achievement")
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

        return new ProgressAndAchievementsDto(progressDtos, achievementDtos);
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

        var memberDtos = memberInfo.Select(mi =>
        {
            var memberGoals = challenge.Goals.Select(g =>
            {
                var p = allProgress.FirstOrDefault(pr => pr.ChallengeGoalId == g.Id && pr.UserId == mi.UserId);
                return p != null ? MapProgressDto(p, g) : new GoalProgressDto(
                    Guid.Empty, g.Id, g.Description, g.Type,
                    g.TargetValue, g.Unit, 0, false, null);
            }).ToList();

            return new MemberProgressDto(mi.UserId, mi.Email, memberGoals);
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
            e.Activity.Goal.Description,
            e.Activity.Goal.Type,
            e.Amount,
            e.Unit,
            e.Notes,
            e.RecordedAt
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
            p.Id, g.Id, g.Description, g.Type,
            g.TargetValue, g.Unit,
            p.CurrentValue, p.IsCompleted, p.CompletedAt
        );
    }
}
