namespace Glasstrut.Api.Models;

public class ChallengeActivityGoal
{
    public Guid ChallengeActivityId { get; set; }
    public ChallengeActivity Activity { get; set; } = null!;
    public Guid ChallengeGoalId { get; set; }
    public ChallengeGoal Goal { get; set; } = null!;
}
