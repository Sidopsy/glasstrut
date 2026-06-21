namespace Glasstrut.Api.Models;

public class ChallengeGoal
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public Challenge Challenge { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "Achievement";
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public bool IsHidden { get; set; }
    public bool IsPerEntry { get; set; }
    public string MetricCategory { get; set; } = "Count";
    public DateTime CreatedAt { get; set; }
    public ICollection<ChallengeActivityGoal> ActivityLinks { get; set; } = new List<ChallengeActivityGoal>();
    public ICollection<ChallengePrize> Prizes { get; set; } = new List<ChallengePrize>();
}
