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
            goals = new[] { "Book 1", "Book 2" },
            prizes = new[] { "Ice cream" }
        };

        var response = await _client.PostAsJsonAsync("/api/challenges", body);
        response.EnsureSuccessStatusCode();

        var challenge = await response.Content.ReadFromJsonAsync<ChallengeResponse>();
        Assert.NotNull(challenge);
        Assert.Equal("Read 10 Books", challenge.Title);
        Assert.Equal("SelfOnly", challenge.Type);
        Assert.Equal(2, challenge.Goals.Count);
        Assert.Single(challenge.Prizes);
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
            goals = new[] { "Living room", "Kitchen" },
            prizes = new[] { "Pizza night" }
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

        // Get the second user's ID from token
        var jwtPayload = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(token2.Split('.')[1].PadRight(token2.Split('.')[1].Length % 4 == 0 ? 0 : 4, '=')));
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

        var body = new { title = "My Challenge", description = "Do it", type = "SelfOnly" };
        await _client.PostAsJsonAsync("/api/challenges", body);

        var response = await _client.GetAsync("/api/challenges");
        response.EnsureSuccessStatusCode();

        var challenges = await response.Content.ReadFromJsonAsync<List<ChallengeResponse>>();
        Assert.Single(challenges!);
    }

    private record AuthResponse(string Token, string Email);
    private record FamilyResponse(Guid Id, string Name, string InviteCode, DateTime CreatedAt, List<MemberResponse> Members);
    private record MemberResponse(string UserId, string Email, string Role);
    private record ChallengeResponse(Guid Id, string Title, string Description, string Type, Guid? FamilyId,
        DateTime? StartDate, DateTime? EndDate, DateTime CreatedAt,
        List<GoalResponse> Goals, List<PrizeResponse> Prizes, List<string> TargetUserIds);
    private record GoalResponse(Guid Id, string Description);
    private record PrizeResponse(Guid Id, string Description);
}
