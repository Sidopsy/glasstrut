using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Glasstrut.Api.Tests;

public class FamilyEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FamilyEndpointTests(CustomWebApplicationFactory factory)
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
    public async Task CreateFamily_ReturnsFamily()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var form = new Dictionary<string, string> { { "name", "Test Family" } };
        var response = await _client.PostAsync("/api/families", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<FamilyResponse>();
        Assert.NotNull(body);
        Assert.Equal("Test Family", body.Name);
        Assert.NotEmpty(body.InviteCode);
        Assert.Single(body.Members);
    }

    [Fact]
    public async Task CreateFamily_RequiresAuth()
    {
        var form = new Dictionary<string, string> { { "name", "Test Family" } };
        var response = await _client.PostAsync("/api/families", new FormUrlEncodedContent(form));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task JoinFamily_ByInviteCode()
    {
        var token1 = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);

        var createForm = new Dictionary<string, string> { { "name", "My Family" } };
        var createResponse = await _client.PostAsync("/api/families", new FormUrlEncodedContent(createForm));
        var family = await createResponse.Content.ReadFromJsonAsync<FamilyResponse>();

        var token2 = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

        var joinForm = new Dictionary<string, string> { { "inviteCode", family!.InviteCode } };
        var joinResponse = await _client.PostAsync("/api/families/join", new FormUrlEncodedContent(joinForm));
        joinResponse.EnsureSuccessStatusCode();

        var joined = await joinResponse.Content.ReadFromJsonAsync<FamilyResponse>();
        Assert.Equal(2, joined!.Members.Count);
    }

    [Fact]
    public async Task JoinFamily_InvalidCode_Returns400()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var form = new Dictionary<string, string> { { "inviteCode", "INVALID" } };
        var response = await _client.PostAsync("/api/families/join", new FormUrlEncodedContent(form));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListFamilies_ReturnsUserFamilies()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var form = new Dictionary<string, string> { { "name", "Family A" } };
        await _client.PostAsync("/api/families", new FormUrlEncodedContent(form));

        var response = await _client.GetAsync("/api/families");
        response.EnsureSuccessStatusCode();

        var families = await response.Content.ReadFromJsonAsync<List<FamilyResponse>>();
        Assert.Single(families!);
    }

    [Fact]
    public async Task GetFamily_ReturnsFamilyDetails()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var form = new Dictionary<string, string> { { "name", "My Family" } };
        var createResponse = await _client.PostAsync("/api/families", new FormUrlEncodedContent(form));
        var created = await createResponse.Content.ReadFromJsonAsync<FamilyResponse>();

        var getResponse = await _client.GetAsync($"/api/families/{created!.Id}");
        getResponse.EnsureSuccessStatusCode();

        var body = await getResponse.Content.ReadFromJsonAsync<FamilyResponse>();
        Assert.Equal(created.Id, body!.Id);
        Assert.Single(body.Members);
    }

    [Fact]
    public async Task NonMember_CannotViewFamily()
    {
        var token1 = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);

        var form = new Dictionary<string, string> { { "name", "Secret Family" } };
        var createResponse = await _client.PostAsync("/api/families", new FormUrlEncodedContent(form));
        var created = await createResponse.Content.ReadFromJsonAsync<FamilyResponse>();

        var token2 = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

        var getResponse = await _client.GetAsync($"/api/families/{created!.Id}");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, getResponse.StatusCode);
    }

    private record FamilyResponse(
        Guid Id,
        string Name,
        string InviteCode,
        DateTime CreatedAt,
        List<MemberResponse> Members
    );

    private record MemberResponse(string UserId, string Email, string Role);

    private record AuthResponse(string Token, string Email);
}
