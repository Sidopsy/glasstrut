namespace Glasstrut.Api.Models.Dtos;

public record RegisterRequest(string Email, string Password, string? Username = null);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, string Email, string? UserName = null);
