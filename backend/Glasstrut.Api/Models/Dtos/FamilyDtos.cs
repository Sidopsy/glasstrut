namespace Glasstrut.Api.Models.Dtos;

public record CreateFamilyRequest(string Name);

public record JoinFamilyRequest(string InviteCode);

public record FamilyDto(
    Guid Id,
    string Name,
    string InviteCode,
    DateTime CreatedAt,
    List<FamilyMemberDto> Members
);

public record FamilyMemberDto(string UserId, string Email, string Role);
