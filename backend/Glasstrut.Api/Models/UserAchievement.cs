namespace Glasstrut.Api.Models;

public class UserAchievement
{
    public Guid Id { get; set; }
    public Guid AchievementId { get; set; }
    public Achievement Achievement { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public DateTime UnlockedAt { get; set; }
}
