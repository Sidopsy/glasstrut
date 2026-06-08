namespace Glasstrut.Api.Models;

public class Family
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public User CreatedBy { get; set; } = null!;
    public ICollection<FamilyMember> Members { get; set; } = new List<FamilyMember>();
}
