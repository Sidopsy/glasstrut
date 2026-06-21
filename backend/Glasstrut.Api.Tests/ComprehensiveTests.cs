using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Glasstrut.Api.Tests;

public class ComprehensiveTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ComprehensiveTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ========== Auth Flows ==========

    [Fact]
    public async Task T001_Auth_Register_And_Login()
    {
        var email = $"user{Guid.NewGuid()}@example.com";
        var resp = await RegisterAsync(email, "Pass123!");
        Assert.Equal(email, resp.Email);
        Assert.NotEmpty(resp.Token);
    }

    [Fact]
    public async Task T002_Auth_Login_WrongPassword_Returns401()
    {
        var email = $"user{Guid.NewGuid()}@example.com";
        await RegisterAsync(email, "Pass123!");

        var form = new Dictionary<string, string> { { "email", email }, { "password", "WRONG" } };
        var resp = await _client.PostAsync("/api/auth/login", new FormUrlEncodedContent(form));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task T003_Auth_DuplicateEmail_Returns400()
    {
        var email = $"dup{Guid.NewGuid()}@example.com";
        await RegisterAsync(email, "Pass123!");

        var form = new Dictionary<string, string> { { "email", email }, { "password", "Pass456!" } };
        var resp = await _client.PostAsync("/api/auth/register", new FormUrlEncodedContent(form));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task T004_Auth_ProtectedEndpoint_WithoutToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/challenges");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task T005_Auth_InvalidToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        var resp = await _client.GetAsync("/api/challenges");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ========== Family Flows ==========

    [Fact]
    public async Task T006_Auth_Register_WithUsername()
    {
        var email = $"user{Guid.NewGuid()}@example.com";
        var form = new Dictionary<string, string> { { "email", email }, { "password", "Pass123!" }, { "username", "cooluser" } };
        var resp = await _client.PostAsync("/api/auth/register", new FormUrlEncodedContent(form));
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("cooluser", body!.UserName);
    }

    [Fact]
    public async Task T007_Auth_Login_WithUsername()
    {
        var email = $"user{Guid.NewGuid()}@example.com";
        var form = new Dictionary<string, string> { { "email", email }, { "password", "Pass123!" }, { "username", "loginuser" } };
        var regResp = await _client.PostAsync("/api/auth/register", new FormUrlEncodedContent(form));
        regResp.EnsureSuccessStatusCode();

        // Login with username instead of email
        var loginForm = new Dictionary<string, string> { { "email", "loginuser" }, { "password", "Pass123!" } };
        var loginResp = await _client.PostAsync("/api/auth/login", new FormUrlEncodedContent(loginForm));
        loginResp.EnsureSuccessStatusCode();
        var body = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("loginuser", body!.UserName);
    }

    [Fact]
    public async Task T008_Auth_Login_WithEmail_StillWorks()
    {
        var email = $"user{Guid.NewGuid()}@example.com";
        var form = new Dictionary<string, string> { { "email", email }, { "password", "Pass123!" } };
        var regResp = await _client.PostAsync("/api/auth/register", new FormUrlEncodedContent(form));
        regResp.EnsureSuccessStatusCode();

        var loginForm = new Dictionary<string, string> { { "email", email }, { "password", "Pass123!" } };
        var loginResp = await _client.PostAsync("/api/auth/login", new FormUrlEncodedContent(loginForm));
        loginResp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task T009_Auth_DuplicateUsername_Returns400()
    {
        var email1 = $"user{Guid.NewGuid()}@example.com";
        var regForm1 = new Dictionary<string, string> { { "email", email1 }, { "password", "Pass123!" }, { "username", "dupuser" } };
        var regResp1 = await _client.PostAsync("/api/auth/register", new FormUrlEncodedContent(regForm1));
        regResp1.EnsureSuccessStatusCode();

        var email2 = $"user{Guid.NewGuid()}@example.com";
        var regForm2 = new Dictionary<string, string> { { "email", email2 }, { "password", "Pass123!" }, { "username", "dupuser" } };
        var regResp2 = await _client.PostAsync("/api/auth/register", new FormUrlEncodedContent(regForm2));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, regResp2.StatusCode);
    }

    [Fact]
    public async Task T010_Family_Create_And_List()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var family = await CreateFamilyAsync("My Fam");

        var listResp = await _client.GetAsync("/api/families");
        var families = await listResp.Content.ReadFromJsonAsync<List<FamilyResponse>>();
        Assert.NotEmpty(families!);
        Assert.Contains(families!, f => f.Name == "My Fam");
    }

    [Fact]
    public async Task T011_Family_Create_RequiresAuth()
    {
        var form = new Dictionary<string, string> { { "name", "NoAuth" } };
        var resp = await _client.PostAsync("/api/families", new FormUrlEncodedContent(form));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task T012_Family_Join_ByInviteCode()
    {
        var t1 = await RegisterAndGetTokenAsync();
        SetAuth(t1);
        var family = await CreateFamilyAsync("Join Family");

        var t2 = await RegisterAndGetTokenAsync();
        SetAuth(t2);
        var joinForm = new Dictionary<string, string> { { "inviteCode", family.InviteCode } };
        var joinResp = await _client.PostAsync("/api/families/join", new FormUrlEncodedContent(joinForm));
        joinResp.EnsureSuccessStatusCode();
        var joined = await joinResp.Content.ReadFromJsonAsync<FamilyResponse>();
        Assert.Equal(2, joined!.Members.Count);
    }

    [Fact]
    public async Task T013_Family_Join_InvalidCode_Returns400()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);
        var form = new Dictionary<string, string> { { "inviteCode", "BADCODE" } };
        var resp = await _client.PostAsync("/api/families/join", new FormUrlEncodedContent(form));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task T014_Family_GetById()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);
        var family = await CreateFamilyAsync("Get Test");

        var resp = await _client.GetAsync($"/api/families/{family.Id}");
        var body = await resp.Content.ReadFromJsonAsync<FamilyResponse>();
        Assert.Equal(family.Id, body!.Id);
        Assert.Single(body.Members);
    }

    [Fact]
    public async Task T015_Family_NonMember_CannotView()
    {
        var t1 = await RegisterAndGetTokenAsync();
        SetAuth(t1);
        var family = await CreateFamilyAsync("Secret");

        var t2 = await RegisterAndGetTokenAsync();
        SetAuth(t2);
        var resp = await _client.GetAsync($"/api/families/{family.Id}");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ========== Challenge Flows ==========

    [Fact]
    public async Task T020_Challenge_Create_SelfOnly()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var challenge = await CreateSimpleChallengeAsync("Solo", "SelfOnly");
        Assert.Equal("Solo", challenge.Title);
        Assert.Equal("SelfOnly", challenge.Type);
        Assert.Single(challenge.Goals);
        Assert.Single(challenge.Prizes);
    }

    [Fact]
    public async Task T021_Challenge_Create_FamilyWide()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);
        var family = await CreateFamilyAsync("Fam");

        var challenge = await CreateFamilyChallengeAsync("Together", family.Id);
        Assert.Equal("Together", challenge.Title);
        Assert.Equal("FamilyWide", challenge.Type);
        Assert.Equal(family.Id, challenge.FamilyId);
    }

    [Fact]
    public async Task T022_Challenge_NonMember_CannotCreateFamilyChallenge()
    {
        var t1 = await RegisterAndGetTokenAsync();
        SetAuth(t1);
        var family = await CreateFamilyAsync("Private");

        var t2 = await RegisterAndGetTokenAsync();
        SetAuth(t2);
        var body = new { title = "Hack", description = "", type = "FamilyWide", familyId = family.Id };
        var resp = await _client.PostAsJsonAsync("/api/challenges", body);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task T023_Challenge_Create_WithCurrency()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Chores",
            description = "Earn points",
            type = "SelfOnly",
            currencyName = "Stars",
            goals = new[]
            {
                new
                {
                    description = "Earn Stars",
                    type = "Currency"
                }
            },
            activities = new[]
            {
                new { name = "Clean", unit = "times", pointValue = 5m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[]
            {
                new { description = "Treat", cost = 10m, hasQR = true }
            }
        };

        var resp = await _client.PostAsJsonAsync("/api/challenges", body);
        resp.EnsureSuccessStatusCode();
        var challenge = await resp.Content.ReadFromJsonAsync<ChallengeResponse>();
        Assert.Equal("Stars", challenge!.CurrencyName);
        Assert.Equal("Currency", challenge.Goals[0].Type);
        Assert.Equal(5, challenge.Activities[0].PointValue);
        Assert.Single(challenge.Prizes);
        Assert.Equal(10, challenge.Prizes[0].Cost);
    }

    [Fact]
    public async Task T024_Challenge_Create_WithHiddenGoal()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Secret Mission",
            description = "Find the surprise",
            type = "SelfOnly",
            goals = new[]
            {
                new
                {
                    description = "Visible goal",
                    type = "Achievement",
                    targetValue = 10m,
                    unit = "tasks",
                    isHidden = false
                },
                new
                {
                    description = "Hidden goal",
                    type = "Achievement",
                    targetValue = 5m,
                    unit = "tasks",
                    isHidden = true
                }
            },
            activities = new[]
            {
                new { name = "Task", unit = "tasks", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 0 } },
                new { name = "Secret", unit = "tasks", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 1 } }
            },
            prizes = new[]
            {
                new { description = "Hidden Prize", cost = (decimal?)null, hasQR = false, challengeGoalId = (Guid?)null }
            }
        };

        var resp = await _client.PostAsJsonAsync("/api/challenges", body);
        resp.EnsureSuccessStatusCode();
        var challenge = await resp.Content.ReadFromJsonAsync<ChallengeResponse>();
        Assert.Equal(2, challenge!.Goals.Count);
        Assert.False(challenge.Goals[0].IsHidden);
        Assert.True(challenge.Goals[1].IsHidden);
    }

    [Fact]
    public async Task T025_Challenge_List()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);
        await CreateSimpleChallengeAsync("A", "SelfOnly");
        await CreateSimpleChallengeAsync("B", "SelfOnly");

        var resp = await _client.GetAsync("/api/challenges");
        var list = await resp.Content.ReadFromJsonAsync<List<ChallengeResponse>>();
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task T026_Challenge_GetById()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);
        var c = await CreateSimpleChallengeAsync("GetMe", "SelfOnly");

        var resp = await _client.GetAsync($"/api/challenges/{c.Id}");
        var challenge = await resp.Content.ReadFromJsonAsync<ChallengeResponse>();
        Assert.Equal(c.Id, challenge!.Id);
    }

    [Fact]
    public async Task T027_Challenge_Update_SelfOnly()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);
        var c = await CreateSimpleChallengeAsync("Original", "SelfOnly");

        var updateBody = new
        {
            title = "Updated",
            description = "Changed",
            startDate = (DateTime?)null,
            endDate = (DateTime?)null,
            goals = c.Goals.Select(g => new
            {
                id = g.Id,
                description = g.Description,
                type = g.Type,
                targetValue = g.TargetValue,
                unit = g.Unit,
                isHidden = false
            }).ToList(),
            activities = c.Activities?.Select(a => new
            {
                id = a.Id,
                name = a.Name,
                unit = a.Unit,
                pointValue = a.PointValue,
                activityType = "Occurrence",
                goalIndices = new[] { 0 }
            }).ToList(),
            prizes = c.Prizes.Select(p => new { id = p.Id, description = p.Description, cost = p.Cost, hasQR = true, challengeGoalId = (Guid?)null }).ToList(),
            currencyName = (string?)null
        };

        var putResp = await _client.PutAsJsonAsync($"/api/challenges/{c.Id}", updateBody);
        putResp.EnsureSuccessStatusCode();
        var updated = await putResp.Content.ReadFromJsonAsync<ChallengeResponse>();
        Assert.Equal("Updated", updated!.Title);
        Assert.Equal("Changed", updated.Description);
    }

    [Fact]
    public async Task T028_Challenge_NonCreator_CannotUpdate()
    {
        var t1 = await RegisterAndGetTokenAsync();
        SetAuth(t1);
        var c = await CreateSimpleChallengeAsync("Mine", "SelfOnly");

        var t2 = await RegisterAndGetTokenAsync();
        SetAuth(t2);
        var updateBody = new { title = "Hacked", description = "", startDate = (DateTime?)null, endDate = (DateTime?)null, goals = new[] { new { id = (Guid?)null, description = "x", type = "Achievement", targetValue = (decimal?)null, unit = (string?)null, isHidden = false } }, activities = new[] { new { id = (Guid?)null, name = "x", unit = "x", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 0 } } }, prizes = Array.Empty<object>(), currencyName = (string?)null };
        var resp = await _client.PutAsJsonAsync($"/api/challenges/{c.Id}", updateBody);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ========== Goal / Activity / Progress Flows ==========

    [Fact]
    public async Task T030_Goal_LogActivity_And_Complete()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);
        var c = await CreateSimpleChallengeAsync("Run", "SelfOnly");

        var aId = c.Activities[0].Id;
        var logResp = await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 50m });
        logResp.EnsureSuccessStatusCode();
        var result = await logResp.Content.ReadFromJsonAsync<LogActivityResponse>();
        Assert.Equal(50, result!.Progress.CurrentValue);
        Assert.False(result.Progress.IsCompleted);

        var logResp2 = await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 50m });
        logResp2.EnsureSuccessStatusCode();
        var result2 = await logResp2.Content.ReadFromJsonAsync<LogActivityResponse>();
        Assert.Equal(100, result2!.Progress.CurrentValue);
        Assert.True(result2.Progress.IsCompleted);
        Assert.NotNull(result2.Progress.CompletedAt);
    }

    [Fact]
    public async Task T031_Goal_LogActivity_DistanceAndTime()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "DistTime",
            description = "Test",
            type = "SelfOnly",
            goals = new[]
            {
                new
                {
                    description = "Run with time",
                    type = "Achievement",
                    targetValue = 100m,
                    unit = "km",
                    metricCategory = "Distance"
                }
            },
            activities = new[]
            {
                new { name = "Running", unit = "km", pointValue = 1m, activityType = "DistanceAndTime", goalIndices = new[] { 0 } }
            }
        };
        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();

        var aId = c!.Activities[0].Id;
        var logResp = await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 10m, timeAmount = 30m });
        logResp.EnsureSuccessStatusCode();
        var result = await logResp.Content.ReadFromJsonAsync<LogActivityResponse>();
        Assert.Equal(10, result!.Progress.CurrentValue);
    }

    [Fact]
    public async Task T032_Goal_LogActivity_LogsNotes()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);
        var c = await CreateSimpleChallengeAsync("Notes", "SelfOnly");

        var aId = c.Activities[0].Id;
        var logResp = await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 10m, notes = "Great session!" });
        logResp.EnsureSuccessStatusCode();

        var logResp2 = await _client.GetAsync($"/api/challenges/{c.Id}/activity-log?count=5");
        var entries = await logResp2.Content.ReadFromJsonAsync<List<ActivityLogEntryResponse>>();
        Assert.NotEmpty(entries!);
        Assert.Contains(entries!, e => e.Notes == "Great session!");
    }

    [Fact]
    public async Task T033_Goal_CompletingGoal_AwardsAchievement()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Read Books",
            description = "Read 10 books",
            type = "SelfOnly",
            goals = new[]
            {
                new
                {
                    description = "Read 10 books",
                    type = "Achievement",
                    targetValue = 10m,
                    unit = "books"
                }
            },
            activities = new[]
            {
                new { name = "Reading", unit = "books", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            }
        };
        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();
        var aId = c!.Activities[0].Id;

        await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 10m });

        var achResp = await _client.GetAsync("/api/achievements");
        var achievements = await achResp.Content.ReadFromJsonAsync<List<AchievementResponse>>();
        Assert.NotEmpty(achievements!);
        Assert.Contains(achievements!, a => a.Title.Contains("Read Books"));
    }

    [Fact]
    public async Task T034_Goal_HiddenGoal_ReturnsSurprise()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Surprise",
            description = "Hidden goal test",
            type = "SelfOnly",
            goals = new[]
            {
                new
                {
                    description = "Hidden surprise",
                    type = "Achievement",
                    targetValue = 5m,
                    unit = "tasks",
                    isHidden = true
                }
            },
            activities = new[]
            {
                new { name = "Secret", unit = "tasks", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[]
            {
                new { description = "Surprise Prize", cost = (decimal?)null, hasQR = false, challengeGoalId = (Guid?)null }
            }
        };

        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();
        var aId = c!.Activities[0].Id;

        // Log partial
        var log1 = await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 3m });
        var r1 = await log1.Content.ReadFromJsonAsync<LogActivityResponse>();
        Assert.Null(r1!.Surprise);

        // Complete
        var log2 = await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 2m });
        var r2 = await log2.Content.ReadFromJsonAsync<LogActivityResponse>();
        Assert.NotNull(r2!.Surprise);
        Assert.Contains("hidden", r2.Surprise.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task T035_Goal_HiddenGoal_HiddenFromProgress_UntilCompleted()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Hidden test",
            description = "test",
            type = "SelfOnly",
            goals = new[]
            {
                new
                {
                    description = "Visible",
                    type = "Achievement",
                    targetValue = 10m,
                    unit = "x",
                    isHidden = false
                },
                new
                {
                    description = "Secret",
                    type = "Achievement",
                    targetValue = 5m,
                    unit = "x",
                    isHidden = true
                }
            },
            activities = new[]
            {
                new { name = "Visible", unit = "x", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 0 } },
                new { name = "Secret", unit = "x", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 1 } }
            }
        };

        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();

        // Progress should only show the visible goal initially
        var progResp = await _client.GetAsync($"/api/challenges/{c!.Id}/progress");
        var prog = await progResp.Content.ReadFromJsonAsync<ProgressResponse>();
        Assert.Single(prog!.Progress);
        Assert.Equal("Visible", prog.Progress[0].GoalDescription);

        // Complete hidden goal
        var hId = c.Activities[1].Id;
        await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{hId}/log", new { amount = 5m });

        // Now progress should show both
        var progResp2 = await _client.GetAsync($"/api/challenges/{c.Id}/progress");
        var prog2 = await progResp2.Content.ReadFromJsonAsync<ProgressResponse>();
        Assert.Equal(2, prog2!.Progress.Count);
    }

    [Fact]
    public async Task T036_Goal_GetProgress_RequiresAuth()
    {
        var resp = await _client.GetAsync($"/api/challenges/{Guid.NewGuid()}/progress");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task T037_Goal_LogActivity_RequiresAuth()
    {
        var resp = await _client.PostAsJsonAsync($"/api/challenges/{Guid.NewGuid()}/activities/{Guid.NewGuid()}/log", new { amount = 10m });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task T038_Goal_ActivityLog_ReturnsEntries()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);
        var c = await CreateSimpleChallengeAsync("LogTest", "SelfOnly");

        var aId = c.Activities[0].Id;
        await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 10m });
        await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 20m });

        var logResp = await _client.GetAsync($"/api/challenges/{c.Id}/activity-log?count=10");
        var entries = await logResp.Content.ReadFromJsonAsync<List<ActivityLogEntryResponse>>();
        Assert.Equal(2, entries!.Count);
    }

    [Fact]
    public async Task T039_Goal_MemberProgress_ForFamilyChallenge()
    {
        var t1 = await RegisterAndGetTokenAsync();
        SetAuth(t1);
        var family = await CreateFamilyAsync("Prog Fam");

        var t2 = await RegisterAndGetTokenAsync();
        SetAuth(t2);
        var joinForm = new Dictionary<string, string> { { "inviteCode", family.InviteCode } };
        await _client.PostAsync("/api/families/join", new FormUrlEncodedContent(joinForm));

        SetAuth(t1);
        var c = await CreateFamilyChallengeAsync("Family Prog", family.Id);
        var aId = c.Activities[0].Id;
        await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 5m });

        var progResp = await _client.GetAsync($"/api/challenges/{c.Id}/progress/members");
        var members = await progResp.Content.ReadFromJsonAsync<ChallengeProgressMembersResponse>();
        Assert.Equal(2, members!.Members.Count);
        Assert.True(members.Members[0].Goals.Count > 0);
    }

    // ========== Currency / Streak Flows ==========

    [Fact]
    public async Task T040_Currency_LogActivity_ReturnsCurrencyEarned()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Currency Test",
            description = "Test currency earning",
            type = "SelfOnly",
            currencyName = "Coins",
            goals = new[]
            {
                new
                {
                    description = "Earn Coins",
                    type = "Achievement",
                    targetValue = 100m,
                    unit = "tasks"
                }
            },
            activities = new[]
            {
                new { name = "Task", unit = "tasks", pointValue = 5m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[] { new { description = "Reward", cost = (decimal?)null, hasQR = false, challengeGoalId = (Guid?)null } }
        };

        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();

        var logResp = await _client.PostAsJsonAsync($"/api/challenges/{c!.Id}/activities/{c.Activities[0].Id}/log",
            new { amount = 3m });
        var result = await logResp.Content.ReadFromJsonAsync<LogActivityResponse>();

        Assert.NotNull(result);
        Assert.True(result.CurrencyEarned > 0);
        Assert.Equal(15m, result.CurrencyEarned); // 3 × 5 = 15
    }

    [Fact]
    public async Task T041_Currency_Progress_ReturnsBalanceAndStreak()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Streak Test",
            description = "Test streak tracking",
            type = "SelfOnly",
            currencyName = "Stars",
            goals = new[]
            {
                new
                {
                    description = "Earn Stars",
                    type = "Achievement",
                    targetValue = 50m,
                    unit = "tasks"
                }
            },
            activities = new[]
            {
                new { name = "Task", unit = "tasks", pointValue = 2m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[] { new { description = "Star Prize", cost = (decimal?)null, hasQR = false, challengeGoalId = (Guid?)null } }
        };

        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();

        // Log activity once
        await _client.PostAsJsonAsync($"/api/challenges/{c!.Id}/activities/{c.Activities[0].Id}/log",
            new { amount = 5m });

        // Check progress
        var progResp = await _client.GetAsync($"/api/challenges/{c.Id}/progress");
        var progress = await progResp.Content.ReadFromJsonAsync<ProgressResponse>();

        Assert.NotNull(progress);
        Assert.Equal(10m, progress.CurrencyBalance); // 5 × 2
        Assert.Equal(1, progress.CurrentStreak);
        Assert.Equal("Stars", progress.CurrencyName);
    }

    [Fact]
    public async Task T042_Currency_NonCurrencyChallenge_ReturnsZeroBalance()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var c = await CreateSimpleChallengeAsync("No Currency", "SelfOnly");

        // Log activity
        await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{c.Activities[0].Id}/log",
            new { amount = 5m });

        var progResp = await _client.GetAsync($"/api/challenges/{c.Id}/progress");
        var progress = await progResp.Content.ReadFromJsonAsync<ProgressResponse>();

        Assert.NotNull(progress);
        Assert.Equal(0, progress.CurrencyBalance);
        Assert.Equal(1, progress.CurrentStreak); // streak tracked even without currency
        Assert.Null(progress.CurrencyName);
    }

    [Fact]
    public async Task T043_Currency_Balance_AccumulatesAndDeductsOnRedeem()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Earn & Spend",
            description = "Test",
            type = "SelfOnly",
            currencyName = "Gold",
            goals = new[]
            {
                new
                {
                    description = "Earn Gold",
                    type = "Achievement",
                    targetValue = 100m,
                    unit = "tasks"
                }
            },
            activities = new[]
            {
                new { name = "Task", unit = "tasks", pointValue = 10m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[] { new { description = "Gold Prize", cost = 15m, hasQR = false, challengeGoalId = (Guid?)null } }
        };

        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();

        var aId = c!.Activities[0].Id;

        // Log two activities: 2 × 10 = 20
        await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 1m });
        await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 1m });

        var progResp = await _client.GetAsync($"/api/challenges/{c.Id}/progress");
        var progress = await progResp.Content.ReadFromJsonAsync<ProgressResponse>();

        Assert.NotNull(progress);
        Assert.Equal(20m, progress.CurrencyBalance); // (1+1) × 10

        // Redeem prize costing 15
        var redeemResp = await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/prizes/{c.Prizes[0].Id}/redeem", new { });
        redeemResp.EnsureSuccessStatusCode();

        // Check balance deducted
        progResp = await _client.GetAsync($"/api/challenges/{c.Id}/progress");
        progress = await progResp.Content.ReadFromJsonAsync<ProgressResponse>();

        Assert.Equal(5m, progress!.CurrencyBalance); // 20 - 15 = 5
    }

    [Fact]
    public async Task T044_Currency_MemberProgress_ReturnsBalanceAndStreak()
    {
        var token1 = await RegisterAndGetTokenAsync();
        var email2 = $"member{Guid.NewGuid()}@example.com";
        var form = new Dictionary<string, string> { { "email", email2 }, { "password", "Pass123!" } };
        var resp2 = await _client.PostAsync("/api/auth/register", new FormUrlEncodedContent(form));
        var user2 = await resp2.Content.ReadFromJsonAsync<AuthResponse>();

        SetAuth(token1);
        var family = await CreateFamilyAsync("Currency Family");

        // User2 joins the family
        SetAuth(user2!.Token);
        var joinForm = new Dictionary<string, string> { { "inviteCode", family.InviteCode } };
        var joinResp = await _client.PostAsync("/api/families/join", new FormUrlEncodedContent(joinForm));
        joinResp.EnsureSuccessStatusCode();

        SetAuth(token1);
        var body = new
        {
            title = "Family Currency",
            description = "Test",
            type = "FamilyWide",
            familyId = family.Id,
            currencyName = "Gems",
            goals = new[]
            {
                new
                {
                    description = "Earn Gems",
                    type = "Achievement",
                    targetValue = 50m,
                    unit = "tasks"
                }
            },
            activities = new[]
            {
                new { name = "Task", unit = "tasks", pointValue = 3m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[] { new { description = "Gem Prize", cost = (decimal?)null, hasQR = false, challengeGoalId = (Guid?)null } }
        };

        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();

        // User 1 logs activity
        await _client.PostAsJsonAsync($"/api/challenges/{c!.Id}/activities/{c.Activities[0].Id}/log",
            new { amount = 2m });

        // Check member progress
        var membersResp = await _client.GetAsync($"/api/challenges/{c.Id}/progress/members");
        var members = await membersResp.Content.ReadFromJsonAsync<ChallengeProgressMembersResponse>();

        Assert.NotNull(members);
        Assert.Equal(2, members.Members.Count);

        // User who logged activity should have balance
        var activeMember = members.Members.FirstOrDefault(m => m.CurrencyBalance > 0);
        Assert.NotNull(activeMember);
        Assert.Equal(6m, activeMember.CurrencyBalance); // 2 × 3
        Assert.Equal(1, activeMember.CurrentStreak);
        Assert.Equal("Gems", activeMember.CurrencyName);

        // Other member should have 0
        var inactiveMember = members.Members.FirstOrDefault(m => m.CurrencyBalance == 0);
        Assert.NotNull(inactiveMember);
        Assert.Equal(0, inactiveMember.CurrentStreak);
    }

    // ========== Prize / Redemption Flows ==========

    [Fact]
    public async Task T045_Prize_Redeem_CurrencyChallenge()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Earn & Spend",
            description = "Test",
            type = "SelfOnly",
            currencyName = "Coins",
            goals = new[]
            {
                new
                {
                    description = "Earn Coins",
                    type = "Currency"
                }
            },
            activities = new[]
            {
                new { name = "Task", unit = "times", pointValue = 10m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[]
            {
                new { description = "Small Prize", cost = 10m, hasQR = false }
            }
        };

        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();

        // Earn 10 coins
        var aId = c!.Activities[0].Id;
        await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 1m });

        // Redeem
        var redeemResp = await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/prizes/{c.Prizes[0].Id}/redeem", new { });
        redeemResp.EnsureSuccessStatusCode();
        var result = await redeemResp.Content.ReadFromJsonAsync<PrizeRedeemResponse>();
        Assert.Equal("Small Prize", result!.PrizeDescription);
        Assert.Equal(10, result.Cost);
    }

    [Fact]
    public async Task T046_Prize_Redeem_InsufficientFunds_Returns400()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Poor",
            description = "No money",
            type = "SelfOnly",
            currencyName = "Coins",
            goals = new[]
            {
                new
                {
                    description = "Earn",
                    type = "Currency"
                }
            },
            activities = new[]
            {
                new { name = "Task", unit = "times", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[]
            {
                new { description = "Expensive", cost = 100m, hasQR = false }
            }
        };

        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();

        var redeemResp = await _client.PostAsJsonAsync($"/api/challenges/{c!.Id}/prizes/{c.Prizes[0].Id}/redeem", new { });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, redeemResp.StatusCode);
    }

    [Fact]
    public async Task T047_Prize_Redeem_GoalLinkedPrize_RequiresCompletion()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Linked Prize",
            description = "Complete goal first",
            type = "SelfOnly",
            goals = new[]
            {
                new
                {
                    description = "Must complete",
                    type = "Achievement",
                    targetValue = 5m,
                    unit = "tasks",
                    isHidden = false
                }
            },
            activities = new[]
            {
                new { name = "Task", unit = "tasks", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[]
            {
                new { description = "Linked Reward", cost = (decimal?)null, hasQR = false, challengeGoalId = (Guid?)null }
            }
        };

        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();

        // Link the prize to the goal via PUT (since creation can't link unknown goal IDs)
        var goalId = c!.Goals[0].Id;
        var updateBody = new
        {
            title = c.Title,
            description = c.Description,
            startDate = (DateTime?)null,
            endDate = (DateTime?)null,
            goals = c.Goals.Select(g => new
            {
                id = g.Id,
                description = g.Description,
                type = g.Type,
                targetValue = g.TargetValue,
                unit = g.Unit,
                isHidden = false
            }).ToList(),
            activities = c.Activities?.Select(a => new
            {
                id = a.Id,
                name = a.Name,
                unit = a.Unit,
                pointValue = a.PointValue,
                activityType = a.ActivityType,
                goalIndices = new[] { 0 }
            }).ToList(),
            prizes = c.Prizes.Select(p => new
            {
                id = p.Id,
                description = p.Description,
                cost = (decimal?)null,
                hasQR = false,
                challengeGoalId = goalId
            }).ToList(),
            currencyName = (string?)null
        };
        var putResp = await _client.PutAsJsonAsync($"/api/challenges/{c.Id}", updateBody);
        putResp.EnsureSuccessStatusCode();

        // Try to redeem without completing goal — should fail
        var redeemResp = await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/prizes/{c.Prizes[0].Id}/redeem", new { });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, redeemResp.StatusCode);
    }

    [Fact]
    public async Task T048_Prize_Redeem_GoalLinked_AfterCompletion_Succeeds()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Linked OK",
            description = "Complete then redeem",
            type = "SelfOnly",
            goals = new[]
            {
                new
                {
                    description = "Prerequisite",
                    type = "Achievement",
                    targetValue = 3m,
                    unit = "tasks",
                    isHidden = false
                }
            },
            activities = new[]
            {
                new { name = "Task", unit = "tasks", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[]
            {
                new { description = "Free Reward", cost = (decimal?)null, hasQR = false, challengeGoalId = (Guid?)null }
            }
        };

        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();
        var aId = c!.Activities[0].Id;
        var goalId = c.Goals[0].Id;

        // Link the prize to the goal via PUT
        var updateBody = new
        {
            title = c.Title,
            description = c.Description,
            startDate = (DateTime?)null,
            endDate = (DateTime?)null,
            goals = c.Goals.Select(g => new
            {
                id = g.Id,
                description = g.Description,
                type = g.Type,
                targetValue = g.TargetValue,
                unit = g.Unit,
                isHidden = false
            }).ToList(),
            activities = c.Activities?.Select(a => new
            {
                id = a.Id,
                name = a.Name,
                unit = a.Unit,
                pointValue = a.PointValue,
                activityType = a.ActivityType,
                goalIndices = new[] { 0 }
            }).ToList(),
            prizes = c.Prizes.Select(p => new
            {
                id = p.Id,
                description = p.Description,
                cost = (decimal?)null,
                hasQR = false,
                challengeGoalId = goalId
            }).ToList(),
            currencyName = (string?)null
        };
        await _client.PutAsJsonAsync($"/api/challenges/{c.Id}", updateBody);

        // Complete the goal
        await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 3m });

        // Now redeem should work
        var redeemResp = await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/prizes/{c.Prizes[0].Id}/redeem", new { });
        redeemResp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task T049_Prize_QR_Generation()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);
        var c = await CreateSimpleChallengeAsync("QR Test", "SelfOnly");

        // Prize has hasQR=true by default
        var qrResp = await _client.GetAsync($"/api/challenges/{c.Id}/prizes/{c.Prizes[0].Id}/qr");
        qrResp.EnsureSuccessStatusCode();
        Assert.Equal("image/png", qrResp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task T050_Prize_QR_WithoutHasQR_Returns400()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "No QR",
            description = "test",
            type = "SelfOnly",
            goals = new[]
            {
                new
                {
                    description = "Goal",
                    type = "Achievement",
                    targetValue = 1m,
                    unit = "x"
                }
            },
            activities = new[]
            {
                new { name = "Do", unit = "x", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[]
            {
                new { description = "NoQR Prize", cost = (decimal?)null, hasQR = false }
            }
        };

        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();

        var qrResp = await _client.GetAsync($"/api/challenges/{c!.Id}/prizes/{c.Prizes[0].Id}/qr");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, qrResp.StatusCode);
    }

    [Fact]
    public async Task T051_Prize_Claims_List()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var body = new
        {
            title = "Claims Test",
            description = "test",
            type = "SelfOnly",
            currencyName = "Coins",
            goals = new[]
            {
                new
                {
                    description = "Earn",
                    type = "Currency"
                }
            },
            activities = new[]
            {
                new { name = "Task", unit = "times", pointValue = 10m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[]
            {
                new { description = "Redeemable", cost = 10m, hasQR = false }
            }
        };

        var createResp = await _client.PostAsJsonAsync("/api/challenges", body);
        var c = await createResp.Content.ReadFromJsonAsync<ChallengeResponse>();
        var aId = c!.Activities[0].Id;

        // Earn and redeem
        await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/activities/{aId}/log", new { amount = 1m });
        await _client.PostAsJsonAsync($"/api/challenges/{c.Id}/prizes/{c.Prizes[0].Id}/redeem", new { });

        var claimsResp = await _client.GetAsync($"/api/challenges/{c.Id}/claims");
        var claims = await claimsResp.Content.ReadFromJsonAsync<List<PrizeClaimResponse>>();
        Assert.NotEmpty(claims!);
        Assert.Contains(claims!, cl => cl.PrizeDescription == "Redeemable");
    }

    // ========== Achievement Flows ==========

    [Fact]
    public async Task T052_Achievement_List_ReturnsAll()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var resp = await _client.GetAsync("/api/achievements");
        resp.EnsureSuccessStatusCode();
        var achievements = await resp.Content.ReadFromJsonAsync<List<AchievementResponse>>();
        Assert.Empty(achievements!);
    }

    [Fact]
    public async Task T053_Achievement_RequiresAuth()
    {
        var resp = await _client.GetAsync("/api/achievements");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ========== Health ==========

    [Fact]
    public async Task T054_Health_ReturnsOk()
    {
        var resp = await _client.GetAsync("/api/health");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("ok", body);
    }

    // ========== Helpers ==========

    private async Task<AuthResponse> RegisterAsync(string email, string password)
    {
        var form = new Dictionary<string, string> { { "email", email }, { "password", password } };
        var resp = await _client.PostAsync("/api/auth/register", new FormUrlEncodedContent(form));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"test{Guid.NewGuid()}@example.com";
        var result = await RegisterAsync(email, "Password123!");
        return result.Token;
    }

    private void SetAuth(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<FamilyResponse> CreateFamilyAsync(string name)
    {
        var form = new Dictionary<string, string> { { "name", name } };
        var resp = await _client.PostAsync("/api/families", new FormUrlEncodedContent(form));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<FamilyResponse>())!;
    }

    private async Task<ChallengeResponse> CreateSimpleChallengeAsync(string title, string type)
    {
        var body = new
        {
            title,
            description = "Description for " + title,
            type,
            goals = new[]
            {
                new
                {
                    description = "Goal for " + title,
                    type = "Achievement",
                    targetValue = 100m,
                    unit = "points"
                }
            },
            activities = new[]
            {
                new { name = "Activity", unit = "points", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[]
            {
                new { description = "Prize for " + title, cost = (decimal?)null, hasQR = true, challengeGoalId = (Guid?)null }
            }
        };
        var resp = await _client.PostAsJsonAsync("/api/challenges", body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ChallengeResponse>())!;
    }

    private async Task<ChallengeResponse> CreateFamilyChallengeAsync(string title, Guid familyId)
    {
        var body = new
        {
            title,
            description = "Family " + title,
            type = "FamilyWide",
            familyId,
            goals = new[]
            {
                new
                {
                    description = "Goal",
                    type = "Achievement",
                    targetValue = 10m,
                    unit = "tasks"
                }
            },
            activities = new[]
            {
                new { name = "Task", unit = "tasks", pointValue = 1m, activityType = "Occurrence", goalIndices = new[] { 0 } }
            },
            prizes = new[]
            {
                new { description = "Reward", cost = (decimal?)null, hasQR = true, challengeGoalId = (Guid?)null }
            }
        };
        var resp = await _client.PostAsJsonAsync("/api/challenges", body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ChallengeResponse>())!;
    }

    // ========== DTOs ==========

    private record AuthResponse(string Token, string Email, string? UserName = null);
    private record FamilyResponse(Guid Id, string Name, string InviteCode, DateTime CreatedAt, List<MemberResponse> Members);
    private record MemberResponse(string UserId, string Email, string Role);
    private record ActivityResponse(Guid Id, string Name, string Unit, decimal PointValue, string ActivityType);
    private record ChallengeResponse(Guid Id, string Title, string Description, string Type, Guid? FamilyId,
        DateTime? StartDate, DateTime? EndDate, DateTime CreatedAt, string? CurrencyName, string? CreatedById,
        List<GoalResponse> Goals, List<PrizeResponse> Prizes, List<string> TargetUserIds,
        List<ActivityResponse>? Activities);
    private record GoalResponse(Guid Id, string Description, string Type, decimal? TargetValue, string? Unit,
        bool IsHidden);
    private record PrizeResponse(Guid Id, string Description, decimal? Cost, bool HasQR, Guid? ChallengeGoalId);
    private record GoalProgressResponse(Guid Id, Guid GoalId, string GoalDescription, string GoalType,
        decimal? TargetValue, string? Unit, decimal CurrentValue, bool IsCompleted, DateTime? CompletedAt);
    private record SurpriseResponse(string Title, string Description);
    private record LogActivityResponse(GoalProgressResponse Progress, SurpriseResponse? Surprise, decimal? CurrencyEarned = null);
    private record ProgressResponse(List<GoalProgressResponse> Progress, List<AchievementResponse> Achievements,
        decimal CurrencyBalance = 0, int CurrentStreak = 0, string? CurrencyName = null);
    private record AchievementResponse(Guid Id, string Title, string Description, bool IsHidden,
        DateTime CreatedAt, DateTime? UnlockedAt);
    private record ChallengeProgressMembersResponse(List<MemberProgressResponse> Members, List<AchievementResponse> Achievements);
    private record MemberProgressResponse(string UserId, string Email, List<GoalProgressResponse> Goals,
        decimal CurrencyBalance = 0, int CurrentStreak = 0, string? CurrencyName = null);
    private record ActivityLogEntryResponse(Guid Id, string UserEmail, string ActivityName, string GoalDescription,
        string GoalType, decimal Amount, decimal? TimeAmount, string? Unit, string? Notes, DateTime RecordedAt,
        decimal? CurrencyEarned = null);
    private record PrizeRedeemResponse(Guid Id, string PrizeDescription, decimal Cost, string? Notes, DateTime ClaimedAt);
    private record PrizeClaimResponse(Guid Id, string PrizeDescription, decimal? Cost, string UserEmail, string? Notes, DateTime ClaimedAt);
}
