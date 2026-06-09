namespace Glasstrut.Api.Models;

public class ChallengeTarget
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public Challenge Challenge { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
}
