using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Glasstrut.Api.Tests;

public class ChallengeEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ChallengeEndpointTests(CustomWebApplicationFactory factory)
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

    private async Task<Guid> CreateFamilyAsync(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var form = new Dictionary<string, string> { { "name", "Test Family" } };
        var response = await _client.PostAsync("/api/families", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();
        var family = await response.Content.ReadFromJsonAsync<FamilyResponse>();
        return family!.Id;
    }

    [Fact]
    public async Task CreateSelfOnlyChallenge()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            title = "Read 10 Books",
            description = "Read 10 books this month",
            type = "SelfOnly",
            goals = new[] {
                new {
                    description = "Read books",
                    type = "Achievement",
                    targetValue = 10m,
                    unit = "books"
                }
            },
            activities = new[] {
                new { name = "Reading", unit = "books", pointValue = 1m, goalIndices = new[] { 0 } }
            },
            prizes = new[] { new { description = "Ice cream" } }
        };

        var response = await _client.PostAsJsonAsync("/api/challenges", body);
        response.EnsureSuccessStatusCode();

        var challenge = await response.Content.ReadFromJsonAsync<ChallengeResponse>();
        Assert.NotNull(challenge);
        Assert.Equal("Read 10 Books", challenge.Title);
        Assert.Equal("SelfOnly", challenge.Type);
        Assert.Single(challenge.Goals);
        Assert.Single(challenge.Prizes);
        Assert.Single(challenge.Activities);
        Assert.Equal("Reading", challenge.Activities[0].Name);
    }

    [Fact]
    public async Task CreateChallengeWithTargetValues()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            title = "Summer Training",
            description = "Run distances",
            type = "SelfOnly",
            startDate = "2026-06-01T00:00:00Z",
            endDate = "2026-08-31T00:00:00Z",
            goals = new[]
            {
                new {
                    description = "Bronze",
                    type = "Achievement",
                    targetValue = 100m,
                    unit = "km"
                },
                new {
                    description = "Silver",
                    type = "Achievement",
                    targetValue = 150m,
                    unit = "km"
                },
                new {
                    description = "Gold",
                    type = "Achievement",
                    targetValue = 200m,
                    unit = "km"
                },
            },
            activities = new[]
            {
                new { name = "Running", unit = "km", pointValue = 1m, goalIndices = new[] { 0 } },
                new { name = "Running", unit = "km", pointValue = 1m, goalIndices = new[] { 1 } },
                new { name = "Running", unit = "km", pointValue = 1m, goalIndices = new[] { 2 } }
            },
            prizes = new[]
            {
                new { description = "Medal", cost = 50m }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/challenges", body);
        response.EnsureSuccessStatusCode();

        var challenge = await response.Content.ReadFromJsonAsync<ChallengeResponse>();
        Assert.NotNull(challenge);
        Assert.Equal(3, challenge.Goals.Count);
        Assert.Equal("Bronze", challenge.Goals[0].Description);
        Assert.Equal("Achievement", challenge.Goals[0].Type);
        Assert.Equal(100, challenge.Goals[0].TargetValue);
        Assert.Equal("km", challenge.Goals[0].Unit);
        Assert.Equal(3, challenge.Activities.Count);
        Assert.Equal("Running", challenge.Activities[0].Name);
        Assert.Equal(1, challenge.Activities[0].PointValue);
        Assert.Single(challenge.Prizes);
        Assert.Equal(50, challenge.Prizes[0].Cost);
    }

    [Fact]
    public async Task CreateChallengeWithCurrency()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            title = "Chores",
            description = "Summer chores",
            type = "SelfOnly",
            currencyName = "Ice Cream Points",
            goals = new[]
            {
                new {
                    description = "Earn Ice Cream Points",
                    type = "Currency"
                },
            },
            activities = new[]
            {
                new { name = "Clean room", unit = "times", pointValue = 5m, goalIndices = new[] { 0 } },
                new { name = "Wash dishes", unit = "times", pointValue = 3m, goalIndices = new[] { 0 } },
            },
            prizes = new[]
            {
                new { description = "Small cone", cost = 5m },
                new { description = "Large cone", cost = 10m },
            }
        };

        var response = await _client.PostAsJsonAsync("/api/challenges", body);
        response.EnsureSuccessStatusCode();

        var challenge = await response.Content.ReadFromJsonAsync<ChallengeResponse>();
        Assert.NotNull(challenge);
        Assert.Equal("Ice Cream Points", challenge.CurrencyName);
        Assert.Single(challenge.Goals);
        Assert.Equal("Currency", challenge.Goals[0].Type);
        Assert.Equal(2, challenge.Activities.Count);
        Assert.Equal(5, challenge.Activities[0].PointValue);
        Assert.Equal("Clean room", challenge.Activities[0].Name);
        Assert.Equal(2, challenge.Prizes.Count);
        Assert.Equal(10, challenge.Prizes[1].Cost);
    }

    [Fact]
    public async Task CreateFamilyWideChallenge()
    {
        var token = await RegisterAndGetTokenAsync();
        var familyId = await CreateFamilyAsync(token);

        var body = new
        {
            title = "Family Cleanup",
            description = "Clean the house together",
            type = "FamilyWide",
            familyId,
            goals = new[] {
                new {
                    description = "Clean room",
                    type = "Achievement",
                    targetValue = 1m,
                    unit = "room"
                }
            },
            activities = new[] {
                new { name = "Cleaning", unit = "room", pointValue = 1m, goalIndices = new[] { 0 } }
            },
            prizes = new[] { new { description = "Pizza night" } }
        };

        var response = await _client.PostAsJsonAsync("/api/challenges", body);
        response.EnsureSuccessStatusCode();

        var challenge = await response.Content.ReadFromJsonAsync<ChallengeResponse>();
        Assert.NotNull(challenge);
        Assert.Equal("FamilyWide", challenge.Type);
        Assert.Equal(familyId, challenge.FamilyId);
    }

    [Fact]
    public async Task NonMember_CannotCreateFamilyChallenge()
    {
        var token1 = await RegisterAndGetTokenAsync();
        var familyId = await CreateFamilyAsync(token1);

        var token2 = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

        var body = new
        {
            title = "Hack",
            description = "Bad",
            type = "FamilyWide",
            familyId
        };

        var response = await _client.PostAsJsonAsync("/api/challenges", body);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateTargetedChallenge()
    {
        var token1 = await RegisterAndGetTokenAsync();
        var familyId = await CreateFamilyAsync(token1);

        // Register a second user and join the family
        var token2 = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

        // Get family to get invite code
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        var familyResponse = await _client.GetAsync($"/api/families/{familyId}");
        var family = await familyResponse.Content.ReadFromJsonAsync<FamilyResponse>();

        // Join via invite code
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);
        var joinForm = new Dictionary<string, string> { { "inviteCode", family!.InviteCode } };
        await _client.PostAsync("/api/families/join", new FormUrlEncodedContent(joinForm));

        // Get the second user's ID from token (JWT uses base64url)
        var b64 = token2.Split('.')[1].Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
        var jwtPayload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
        var user2Id = System.Text.Json.JsonSerializer
            .Deserialize<System.Text.Json.JsonElement>(jwtPayload)
            .GetProperty("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
            .GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        var body = new
        {
            title = "Math Challenge",
            description = "For specific members",
            type = "Targeted",
            familyId,
            targetUserIds = new[] { user2Id }
        };

        var response = await _client.PostAsJsonAsync("/api/challenges", body);
        response.EnsureSuccessStatusCode();

        var challenge = await response.Content.ReadFromJsonAsync<ChallengeResponse>();
        Assert.NotNull(challenge);
        Assert.Equal("Targeted", challenge.Type);
        Assert.Single(challenge.TargetUserIds);
        Assert.Equal(user2Id, challenge.TargetUserIds[0]);
    }

    [Fact]
    public async Task InvalidTarget_Returns400()
    {
        var token = await RegisterAndGetTokenAsync();
        var familyId = await CreateFamilyAsync(token);

        var body = new
        {
            title = "Bad",
            description = "Bad",
            type = "Targeted",
            familyId,
            targetUserIds = new[] { "nonexistent-user" }
        };

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync("/api/challenges", body);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListSelfOnlyChallenges()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new {
            title = "My Challenge",
            description = "Do it",
            type = "SelfOnly",
            goals = new[] {
                new {
                    description = "Task",
                    type = "Achievement",
                    targetValue = 1m,
                    unit = "task"
                }
            },
            activities = new[] {
                new { name = "Do task", unit = "task", pointValue = 1m, goalIndices = new[] { 0 } }
            }
        };
        await _client.PostAsJsonAsync("/api/challenges", body);

        var response = await _client.GetAsync("/api/challenges");
        response.EnsureSuccessStatusCode();

        var challenges = await response.Content.ReadFromJsonAsync<List<ChallengeResponse>>();
        Assert.Single(challenges!);
        Assert.Equal("My Challenge", challenges![0].Title);
        Assert.Equal("Do it", challenges[0].Description);
    }

    [Fact]
    public async Task GetChallengeById_ReturnsChallengeWithTitleAndDescription()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new {
            title = "Read 20 Books",
            description = "Read 20 books this summer",
            type = "SelfOnly",
            goals = (object[])[],
            prizes = (object[])[],
            activities = (object[])[],
        };
        var createResponse = await _client.PostAsJsonAsync("/api/challenges", body);
        var created = await createResponse.Content.ReadFromJsonAsync<ChallengeResponse>();

        var response = await _client.GetAsync($"/api/challenges/{created!.Id}");
        response.EnsureSuccessStatusCode();
        var challenge = await response.Content.ReadFromJsonAsync<ChallengeResponse>();

        Assert.NotNull(challenge);
        Assert.Equal(created.Id, challenge.Id);
        Assert.Equal("Read 20 Books", challenge.Title);
        Assert.Equal("Read 20 books this summer", challenge.Description);
    }

    private record AuthResponse(string Token, string Email);
    private record FamilyResponse(Guid Id, string Name, string InviteCode, DateTime CreatedAt, List<MemberResponse> Members);
    private record MemberResponse(string UserId, string Email, string Role);
    private record ActivityResponse(Guid Id, string Name, string Unit, decimal PointValue);
    private record ChallengeResponse(Guid Id, string Title, string Description, string Type, Guid? FamilyId,
        DateTime? StartDate, DateTime? EndDate, DateTime CreatedAt, string? CurrencyName,
        List<GoalResponse> Goals, List<PrizeResponse> Prizes, List<string> TargetUserIds,
        List<ActivityResponse>? Activities);
    private record GoalResponse(Guid Id, string Description, string Type, decimal? TargetValue, string? Unit);
    private record PrizeResponse(Guid Id, string Description, decimal? Cost);
}
