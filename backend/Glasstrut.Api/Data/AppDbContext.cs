using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Glasstrut.Api.Models;

namespace Glasstrut.Api.Data;

public class AppDbContext : IdentityDbContext<User>
{
    public DbSet<Family> Families => Set<Family>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<Challenge> Challenges => Set<Challenge>();
    public DbSet<ChallengeGoal> ChallengeGoals => Set<ChallengeGoal>();
    public DbSet<ChallengeActivity> ChallengeActivities => Set<ChallengeActivity>();
    public DbSet<ChallengePrize> ChallengePrizes => Set<ChallengePrize>();
    public DbSet<ChallengeTarget> ChallengeTargets => Set<ChallengeTarget>();
    public DbSet<GoalProgress> GoalProgresses => Set<GoalProgress>();
    public DbSet<ProgressEntry> ProgressEntries => Set<ProgressEntry>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<PrizeClaim> PrizeClaims => Set<PrizeClaim>();
    public DbSet<ChallengeCurrencyBalance> ChallengeCurrencyBalances => Set<ChallengeCurrencyBalance>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Family>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InviteCode).IsUnique();
            entity.HasOne(e => e.CreatedBy)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FamilyMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FamilyId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Family)
                  .WithMany(f => f.Members)
                  .HasForeignKey(e => e.FamilyId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Challenge>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Family)
                  .WithMany()
                  .HasForeignKey(e => e.FamilyId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedBy)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChallengeGoal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Challenge)
                  .WithMany(c => c.Goals)
                  .HasForeignKey(e => e.ChallengeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChallengeActivity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Challenge)
                  .WithMany(c => c.Activities)
                  .HasForeignKey(e => e.ChallengeId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Goal)
                  .WithMany(g => g.Activities)
                  .HasForeignKey(e => e.ChallengeGoalId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChallengePrize>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Challenge)
                  .WithMany(c => c.Prizes)
                  .HasForeignKey(e => e.ChallengeId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Goal)
                  .WithMany(g => g.Prizes)
                  .HasForeignKey(e => e.ChallengeGoalId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ChallengeTarget>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Challenge)
                  .WithMany(c => c.Targets)
                  .HasForeignKey(e => e.ChallengeId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoalProgress>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ChallengeGoalId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Goal)
                  .WithMany()
                  .HasForeignKey(e => e.ChallengeGoalId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProgressEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Activity)
                  .WithMany()
                  .HasForeignKey(e => e.ChallengeActivityId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Achievement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Challenge)
                  .WithMany()
                  .HasForeignKey(e => e.ChallengeId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ChallengeGoal)
                  .WithMany()
                  .HasForeignKey(e => e.ChallengeGoalId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.ChallengeId, e.ChallengeGoalId });
        });

        builder.Entity<PrizeClaim>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ChallengePrizeId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Prize)
                  .WithMany()
                  .HasForeignKey(e => e.ChallengePrizeId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Challenge)
                  .WithMany()
                  .HasForeignKey(e => e.ChallengeId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserAchievement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AchievementId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Achievement)
                  .WithMany(a => a.UserAchievements)
                  .HasForeignKey(e => e.AchievementId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChallengeCurrencyBalance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ChallengeId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Challenge)
                  .WithMany()
                  .HasForeignKey(e => e.ChallengeId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
