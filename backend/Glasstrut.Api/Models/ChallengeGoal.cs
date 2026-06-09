namespace Glasstrut.Api.Models;

public class ChallengeGoal
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public Challenge Challenge { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
