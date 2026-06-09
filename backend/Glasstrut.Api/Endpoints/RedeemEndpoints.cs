using System.Security.Claims;
using Glasstrut.Api.Models.Dtos;
using Glasstrut.Api.Services;

namespace Glasstrut.Api.Endpoints;

public static class RedeemEndpoints
{
    public static void MapRedeemEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapGet("/challenges/{challengeId:guid}/prizes/{prizeId:guid}/qr",
            async (Guid challengeId, Guid prizeId, IRedeemService service, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var bytes = await service.GetPrizeQrAsync(userId, challengeId, prizeId);
                return Results.File(bytes, "image/png");
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

        group.MapPost("/challenges/{challengeId:guid}/prizes/{prizeId:guid}/redeem",
            async (Guid challengeId, Guid prizeId, IRedeemService service, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var result = await service.RedeemPrizeAsync(userId, challengeId, prizeId);
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

        group.MapGet("/challenges/{challengeId:guid}/claims",
            async (Guid challengeId, IRedeemService service, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var result = await service.GetPrizeClaimsAsync(userId, challengeId);
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
    }
}
