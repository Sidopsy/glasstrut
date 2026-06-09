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
    public DbSet<ChallengePrize> ChallengePrizes => Set<ChallengePrize>();
    public DbSet<ChallengeTarget> ChallengeTargets => Set<ChallengeTarget>();
    public DbSet<GoalProgress> GoalProgresses => Set<GoalProgress>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();

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

        builder.Entity<ChallengePrize>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Challenge)
                  .WithMany(c => c.Prizes)
                  .HasForeignKey(e => e.ChallengeId)
                  .OnDelete(DeleteBehavior.Cascade);
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

        builder.Entity<Achievement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Challenge)
                  .WithMany()
                  .HasForeignKey(e => e.ChallengeId)
                  .OnDelete(DeleteBehavior.Cascade);
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
    }
}
