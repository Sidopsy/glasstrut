using System.Security.Claims;
using Glasstrut.Api.Models.Dtos;
using Glasstrut.Api.Services;

namespace Glasstrut.Api.Endpoints;

public static class FamilyEndpoints
{
    public static void MapFamilyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/families").RequireAuthorization();

        group.MapPost("/", async (HttpRequest request, IFamilyService service, ClaimsPrincipal user) =>
        {
            var form = await request.ReadFormAsync();
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Results.Unauthorized();

            var name = form["name"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { error = "Family name is required." });

            try
            {
                var result = await service.CreateFamilyAsync(userId, name);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/join", async (HttpRequest request, IFamilyService service, ClaimsPrincipal user) =>
        {
            var form = await request.ReadFormAsync();
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Results.Unauthorized();

            var inviteCode = form["inviteCode"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(inviteCode))
                return Results.BadRequest(new { error = "Invite code is required." });

            try
            {
                var result = await service.JoinFamilyAsync(userId, inviteCode);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/", async (IFamilyService service, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Results.Unauthorized();
            var families = await service.GetUserFamiliesAsync(userId);
            return Results.Ok(families);
        });

        group.MapGet("/{familyId:guid}", async (Guid familyId, IFamilyService service, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Results.Unauthorized();
            try
            {
                var result = await service.GetFamilyAsync(userId, familyId);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });

        group.MapDelete("/{familyId:guid}/members/{memberUserId}",
            async (Guid familyId, string memberUserId, IFamilyService service, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Results.Unauthorized();
            try
            {
                await service.RemoveMemberAsync(userId, familyId, memberUserId);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });
    }
}
