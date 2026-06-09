namespace Glasstrut.Api.Models;

public class ChallengeActivity
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public Challenge Challenge { get; set; } = null!;
    public Guid ChallengeGoalId { get; set; }
    public ChallengeGoal Goal { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "times";
    public decimal PointValue { get; set; }
    public DateTime CreatedAt { get; set; }
}
