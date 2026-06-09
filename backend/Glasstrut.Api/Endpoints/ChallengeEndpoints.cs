using System.Security.Claims;
using Glasstrut.Api.Models.Dtos;
using Glasstrut.Api.Services;

namespace Glasstrut.Api.Endpoints;

public static class ChallengeEndpoints
{
    public static void MapChallengeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/challenges").RequireAuthorization();

        group.MapPost("/", async (CreateChallengeRequest request, IChallengeService service, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var result = await service.CreateChallengeAsync(userId, request);
                return Results.Created($"/api/challenges/{result.Id}", result);
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

        group.MapGet("/{challengeId:guid}", async (Guid challengeId, IChallengeService service, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var result = await service.GetChallengeAsync(userId, challengeId);
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

        group.MapGet("/", async (Guid? familyId, IChallengeService service, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var result = await service.GetChallengesAsync(userId, familyId);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });
    }
}
