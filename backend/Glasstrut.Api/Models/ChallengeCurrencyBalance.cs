namespace Glasstrut.Api.Models;

public class ChallengeCurrencyBalance
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public Challenge Challenge { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public decimal Balance { get; set; }
    public int CurrentStreak { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public DateTime UpdatedAt { get; set; }
}
