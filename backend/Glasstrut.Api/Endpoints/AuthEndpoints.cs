using Glasstrut.Api.Models.Dtos;
using Glasstrut.Api.Services;

namespace Glasstrut.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (HttpRequest request, IAuthService auth) =>
        {
            var form = await request.ReadFormAsync();
            var dto = new RegisterRequest(form["email"]!, form["password"]!, form["username"]);
            try
            {
                var result = await auth.RegisterAsync(dto);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/auth/login", async (HttpRequest request, IAuthService auth) =>
        {
            var form = await request.ReadFormAsync();
            var dto = new LoginRequest(form["email"]!, form["password"]!);
            try
            {
                var result = await auth.LoginAsync(dto);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
    }
}
