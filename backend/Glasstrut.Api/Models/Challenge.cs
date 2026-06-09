namespace Glasstrut.Api.Models;

public class Challenge
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "SelfOnly";
    public Guid? FamilyId { get; set; }
    public Family? Family { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public User CreatedBy { get; set; } = null!;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? CurrencyName { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<ChallengeGoal> Goals { get; set; } = new List<ChallengeGoal>();
    public ICollection<ChallengePrize> Prizes { get; set; } = new List<ChallengePrize>();
    public ICollection<ChallengeTarget> Targets { get; set; } = new List<ChallengeTarget>();
}
