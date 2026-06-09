namespace Glasstrut.Api.Models;

public class PrizeClaim
{
    public Guid Id { get; set; }
    public Guid ChallengePrizeId { get; set; }
    public ChallengePrize Prize { get; set; } = null!;
    public Guid ChallengeId { get; set; }
    public Challenge Challenge { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime ClaimedAt { get; set; }
}
