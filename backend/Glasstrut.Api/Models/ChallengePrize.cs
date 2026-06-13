namespace Glasstrut.Api.Models;

public class ChallengePrize
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public Challenge Challenge { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal? Cost { get; set; }
    public bool HasQR { get; set; } = true;
    public Guid? ChallengeGoalId { get; set; }
    public ChallengeGoal? Goal { get; set; }
    public DateTime CreatedAt { get; set; }
}
