using System.Security.Claims;
using Glasstrut.Api.Models.Dtos;
using Glasstrut.Api.Services;

namespace Glasstrut.Api.Endpoints;

public static class GoalEndpoints
{
    public static void MapGoalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapPost("/challenges/{challengeId:guid}/goals/{goalId:guid}/progress",
            async (Guid challengeId, Guid goalId, RecordProgressRequest request,
                   IGoalService service, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var result = await service.RecordProgressAsync(userId, goalId, request);
                return Results.Ok(result);
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

        group.MapGet("/challenges/{challengeId:guid}/progress",
            async (Guid challengeId, IGoalService service, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var result = await service.GetChallengeProgressAsync(userId, challengeId);
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

        group.MapGet("/achievements", async (IGoalService service, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await service.GetUserAchievementsAsync(userId);
            return Results.Ok(result);
        });
    }
}
