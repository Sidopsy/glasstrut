using Microsoft.EntityFrameworkCore;
using Glasstrut.Api.Data;
using Glasstrut.Api.Models;
using Glasstrut.Api.Models.Dtos;

namespace Glasstrut.Api.Services;

public class FamilyService : IFamilyService
{
    private readonly AppDbContext _db;

    public FamilyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FamilyDto> CreateFamilyAsync(string userId, string name)
    {
        var inviteCode = await GenerateUniqueInviteCode();

        var family = new Family
        {
            Id = Guid.NewGuid(),
            Name = name,
            InviteCode = inviteCode,
            CreatedAt = DateTime.UtcNow,
            CreatedById = userId,
        };

        family.Members.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            UserId = userId,
            Role = "Admin",
            JoinedAt = DateTime.UtcNow,
        });

        _db.Families.Add(family);
        await _db.SaveChangesAsync();

        return await GetFamilyAsync(userId, family.Id);
    }

    public async Task<FamilyDto> JoinFamilyAsync(string userId, string inviteCode)
    {
        var family = await _db.Families
            .Include(f => f.Members)
            .FirstOrDefaultAsync(f => f.InviteCode == inviteCode)
            ?? throw new InvalidOperationException("Invalid invite code.");

        if (family.Members.Any(m => m.UserId == userId))
            throw new InvalidOperationException("You are already a member of this family.");

        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            UserId = userId,
            Role = "Member",
            JoinedAt = DateTime.UtcNow,
        };

        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync();

        return await GetFamilyAsync(userId, family.Id);
    }

    public async Task<FamilyDto> GetFamilyAsync(string userId, Guid familyId)
    {
        var family = await _db.Families
            .Include(f => f.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(f => f.Id == familyId)
            ?? throw new InvalidOperationException("Family not found.");

        if (family.Members.All(m => m.UserId != userId))
            throw new UnauthorizedAccessException("You are not a member of this family.");

        return MapToDto(family);
    }

    public async Task<List<FamilyDto>> GetUserFamiliesAsync(string userId)
    {
        var families = await _db.Families
            .Include(f => f.Members)
            .ThenInclude(m => m.User)
            .Where(f => f.Members.Any(m => m.UserId == userId))
            .ToListAsync();

        return families.Select(MapToDto).ToList();
    }

    public async Task RemoveMemberAsync(string userId, Guid familyId, string memberUserId)
    {
        var family = await _db.Families
            .Include(f => f.Members)
            .FirstOrDefaultAsync(f => f.Id == familyId)
            ?? throw new InvalidOperationException("Family not found.");

        var currentMember = family.Members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new UnauthorizedAccessException("You are not a member of this family.");

        if (currentMember.Role != "Admin")
            throw new UnauthorizedAccessException("Only family admins can remove members.");

        var target = family.Members.FirstOrDefault(m => m.UserId == memberUserId)
            ?? throw new InvalidOperationException("Member not found.");

        if (target.Role == "Admin")
            throw new InvalidOperationException("Cannot remove an admin.");

        _db.FamilyMembers.Remove(target);
        await _db.SaveChangesAsync();
    }

    private async Task<string> GenerateUniqueInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string code;
        do
        {
            code = new string(Enumerable.Range(0, 8)
                .Select(_ => chars[Random.Shared.Next(chars.Length)])
                .ToArray());
        }
        while (await _db.Families.AnyAsync(f => f.InviteCode == code));

        return code;
    }

    private static FamilyDto MapToDto(Family family)
    {
        return new FamilyDto(
            family.Id,
            family.Name,
            family.InviteCode,
            family.CreatedAt,
            family.Members.Select(m => new FamilyMemberDto(
                m.UserId,
                m.User?.Email ?? "unknown",
                m.Role
            )).ToList()
        );
    }
}
