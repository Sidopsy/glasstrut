namespace Glasstrut.Api.Models;

public class FamilyMember
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Family Family { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public string Role { get; set; } = "Member";
    public DateTime JoinedAt { get; set; }
}
