namespace Glasstrut.Api.Models;

public class ChallengeGoal
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public Challenge Challenge { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public decimal? PointValue { get; set; }
    public DateTime CreatedAt { get; set; }
}
