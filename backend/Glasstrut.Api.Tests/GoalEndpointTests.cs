using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Glasstrut.Api.Tests;

public class GoalEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GoalEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"test{Guid.NewGuid()}@example.com";
        var form = new Dictionary<string, string>
        {
            { "email", email },
            { "password", "Password123!" }
        };
        var response = await _client.PostAsync("/api/auth/register", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    [Fact]
    public async Task LogActivity_And_CompleteGoal()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            title = "Run 100km",
            description = "Training",
            type = "SelfOnly",
            goals = new[] {
                new {
                    description = "Run 100km",
                    type = "Achievement",
                    targetValue = 100m,
                    unit = "km",
                    activities = new[] {
                        new { name = "Running", unit = "km", pointValue = 1m }
                    }
                }
            }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/challenges", body);
        var challenge = await createResponse.Content.ReadFromJsonAsync<ChallengeResponse>();
        var activityId = challenge!.Goals[0].Activities[0].Id;

        // Log 50km — pointValue=1, so delta = 50*1 = 50
        var logResponse = await _client.PostAsJsonAsync(
            $"/api/challenges/{challenge.Id}/activities/{activityId}/log",
            new { amount = 50m });
        logResponse.EnsureSuccessStatusCode();
        var logResult = await logResponse.Content.ReadFromJsonAsync<LogActivityResponse>();
        Assert.Equal(50, logResult!.Progress.CurrentValue);
        Assert.False(logResult.Progress.IsCompleted);

        // Log another 50km — total = 100, goal completes
        var completeResponse = await _client.PostAsJsonAsync(
            $"/api/challenges/{challenge.Id}/activities/{activityId}/log",
            new { amount = 50m });
        completeResponse.EnsureSuccessStatusCode();
        var completeResult = await completeResponse.Content.ReadFromJsonAsync<LogActivityResponse>();
        Assert.Equal(100, completeResult!.Progress.CurrentValue);
        Assert.True(completeResult.Progress.IsCompleted);
        Assert.NotNull(completeResult.Progress.CompletedAt);
    }

    [Fact]
    public async Task GetChallengeProgress_ReturnsAllGoals()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            title = "Summer Training",
            description = "Run distances",
            type = "SelfOnly",
            goals = new[]
            {
                new {
                    description = "Bronze",
                    type = "Achievement",
                    targetValue = 100m,
                    unit = "km",
                    activities = new[] {
                        new { name = "Running", unit = "km", pointValue = 1m }
                    }
                },
                new {
                    description = "Silver",
                    type = "Achievement",
                    targetValue = 150m,
                    unit = "km",
                    activities = new[] {
                        new { name = "Running", unit = "km", pointValue = 1m }
                    }
                },
            }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/challenges", body);
        var challenge = await createResponse.Content.ReadFromJsonAsync<ChallengeResponse>();

        var progressResponse = await _client.GetAsync($"/api/challenges/{challenge!.Id}/progress");
        progressResponse.EnsureSuccessStatusCode();

        var result = await progressResponse.Content.ReadFromJsonAsync<ProgressResponse>();
        Assert.Equal(2, result!.Progress.Count);
        Assert.Equal(0, result.Progress[0].CurrentValue);
        Assert.False(result.Progress[0].IsCompleted);
    }

    [Fact]
    public async Task CompletingGoal_AwardsAchievement()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            title = "Read Books",
            description = "Read 10 books",
            type = "SelfOnly",
            goals = new[] {
                new {
                    description = "Read 10 books",
                    type = "Achievement",
                    targetValue = 10m,
                    unit = "books",
                    activities = new[] {
                        new { name = "Reading", unit = "books", pointValue = 1m }
                    }
                }
            }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/challenges", body);
        var challenge = await createResponse.Content.ReadFromJsonAsync<ChallengeResponse>();
        var activityId = challenge!.Goals[0].Activities[0].Id;

        await _client.PostAsJsonAsync(
            $"/api/challenges/{challenge.Id}/activities/{activityId}/log",
            new { amount = 10m });

        var achievementsResponse = await _client.GetAsync("/api/achievements");
        achievementsResponse.EnsureSuccessStatusCode();
        var achievements = await achievementsResponse.Content.ReadFromJsonAsync<List<AchievementResponse>>();

        Assert.NotEmpty(achievements!);
        Assert.Contains(achievements!, a => a.Title.Contains("Read Books"));
    }

    [Fact]
    public async Task LogProgress_RequiresAuth()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/challenges/{Guid.NewGuid()}/activities/{Guid.NewGuid()}/log",
            new { amount = 10m });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAchievements_ReturnsEmpty_WhenNone()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/achievements");
        response.EnsureSuccessStatusCode();

        var achievements = await response.Content.ReadFromJsonAsync<List<AchievementResponse>>();
        Assert.Empty(achievements!);
    }

    private record AuthResponse(string Token, string Email);
    private record ActivityResponse(Guid Id, string Name, string Unit, decimal PointValue);
    private record ChallengeResponse(Guid Id, string Title, string Description, string Type, Guid? FamilyId,
        DateTime? StartDate, DateTime? EndDate, DateTime CreatedAt, string? CurrencyName,
        List<GoalResponse> Goals, List<PrizeResponse> Prizes, List<string> TargetUserIds);
    private record GoalResponse(Guid Id, string Description, string Type, decimal? TargetValue, string? Unit,
        List<ActivityResponse> Activities);
    private record PrizeResponse(Guid Id, string Description, decimal? Cost);
    private record GoalProgressResponse(Guid Id, Guid GoalId, string GoalDescription, string GoalType,
        decimal? TargetValue, string? Unit, decimal CurrentValue, bool IsCompleted, DateTime? CompletedAt);
    private record SurpriseResponse(string Title, string Description);
    private record LogActivityResponse(GoalProgressResponse Progress, SurpriseResponse? Surprise);
    private record ProgressResponse(List<GoalProgressResponse> Progress, List<AchievementResponse> Achievements);
    private record AchievementResponse(Guid Id, string Title, string Description, bool IsHidden,
        DateTime CreatedAt, DateTime? UnlockedAt);
}
