namespace Glasstrut.Api.Models;

public class ChallengeActivity
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public Challenge Challenge { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string ActivityType { get; set; } = "Occurrence";
    public string Unit { get; set; } = "times";
    public string? TimeUnit { get; set; }
    public decimal PointValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<ChallengeActivityGoal> GoalLinks { get; set; } = new List<ChallengeActivityGoal>();
}
