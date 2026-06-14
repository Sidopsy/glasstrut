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
            var email = form["email"].FirstOrDefault();
            var password = form["password"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return Results.BadRequest(new { error = "Email and password are required." });

            var dto = new RegisterRequest(email, password, form["username"].FirstOrDefault());
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
            var email = form["email"].FirstOrDefault();
            var password = form["password"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return Results.BadRequest(new { error = "Email and password are required." });

            var dto = new LoginRequest(email, password);
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
