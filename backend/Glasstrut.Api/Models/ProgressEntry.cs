namespace Glasstrut.Api.Models;

public class ProgressEntry
{
    public Guid Id { get; set; }
    public Guid ChallengeActivityId { get; set; }
    public ChallengeActivity Activity { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
