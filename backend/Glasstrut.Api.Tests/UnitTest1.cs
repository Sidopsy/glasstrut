using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Glasstrut.Api.Data;

namespace Glasstrut.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(connection));

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }
}

public class HealthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ok", body);
    }

    [Fact]
    public async Task Register_And_Login()
    {
        var email = $"test{Guid.NewGuid()}@example.com";
        var password = "Password123!";

        var registerForm = new Dictionary<string, string>
        {
            { "email", email },
            { "password", password }
        };

        var registerResponse = await _client.PostAsync("/api/auth/register",
            new FormUrlEncodedContent(registerForm));
        registerResponse.EnsureSuccessStatusCode();

        var loginForm = new Dictionary<string, string>
        {
            { "email", email },
            { "password", password }
        };

        var loginResponse = await _client.PostAsync("/api/auth/login",
            new FormUrlEncodedContent(loginForm));
        loginResponse.EnsureSuccessStatusCode();

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginBody);
        Assert.Equal(email, loginBody.Email);
        Assert.NotEmpty(loginBody.Token);
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_401()
    {
        var form = new Dictionary<string, string>
        {
            { "email", "nonexistent@example.com" },
            { "password", "wrong" }
        };

        var response = await _client.PostAsync("/api/auth/login",
            new FormUrlEncodedContent(form));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record AuthResponse(string Token, string Email);
}
