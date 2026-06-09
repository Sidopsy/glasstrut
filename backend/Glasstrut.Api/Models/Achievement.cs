namespace Glasstrut.Api.Models;

public class Achievement
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public Guid? ChallengeId { get; set; }
    public Challenge? Challenge { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
