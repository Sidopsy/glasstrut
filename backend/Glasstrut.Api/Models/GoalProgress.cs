namespace Glasstrut.Api.Models;

public class GoalProgress
{
    public Guid Id { get; set; }
    public Guid ChallengeGoalId { get; set; }
    public ChallengeGoal Goal { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public decimal CurrentValue { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
