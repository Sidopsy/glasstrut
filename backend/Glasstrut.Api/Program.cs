using Glasstrut.Api.Data;
using Glasstrut.Api.Endpoints;
using Glasstrut.Api.Models;
using Glasstrut.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

builder.Services.AddIdentity<User, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

IConfigurationSection jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
    };
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFamilyService, FamilyService>();
builder.Services.AddScoped<IChallengeService, ChallengeService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<IRedeemService, RedeemService>();
builder.Services.AddAuthorization();

builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is { Length: > 0 })
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });
}
else
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });
}

WebApplication app = builder.Build();

// Always run migrations — in dev for local SQLite, in prod for the server
using (IServiceScope scope = app.Services.CreateScope())
{
    AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Ensure TimeUnit column exists (added after initial deployment)
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE ChallengeActivities ADD COLUMN TimeUnit TEXT NULL");
    }
    catch { /* Column may already exist */ }

    // Backfill MetricCategory for existing goals and recalculate progress
    try
    {
        db.Database.ExecuteSqlRaw(@"
            UPDATE ChallengeGoals
            SET MetricCategory = CASE
                WHEN EXISTS (SELECT 1 FROM ChallengeActivityGoal gl
                    INNER JOIN ChallengeActivities a ON gl.ChallengeActivityId = a.Id
                    WHERE gl.ChallengeGoalId = ChallengeGoals.Id AND a.ActivityType = 'Distance')
                THEN 'Distance'
                WHEN EXISTS (SELECT 1 FROM ChallengeActivityGoal gl
                    INNER JOIN ChallengeActivities a ON gl.ChallengeActivityId = a.Id
                    WHERE gl.ChallengeGoalId = ChallengeGoals.Id AND a.ActivityType = 'Time')
                THEN 'Time'
                WHEN EXISTS (SELECT 1 FROM ChallengeActivityGoal gl
                    INNER JOIN ChallengeActivities a ON gl.ChallengeActivityId = a.Id
                    WHERE gl.ChallengeGoalId = ChallengeGoals.Id AND a.ActivityType = 'DistanceAndTime')
                THEN 'Distance'
                ELSE 'Count'
            END
        ");
    }
    catch { /* Column may not exist yet */ }

    // Recalculate GoalProgress.CurrentValue for accumulation goals
    try
    {
        db.Database.ExecuteSqlRaw(@"
            UPDATE GoalProgresses
            SET CurrentValue = (
                SELECT COALESCE(SUM(
                    CASE
                        WHEN a.ActivityType = 'DistanceAndTime' AND g.MetricCategory = 'Time'
                            THEN COALESCE(pe.TimeAmount, 0) * a.PointValue
                        ELSE pe.Amount * a.PointValue
                    END
                ), 0)
                FROM ProgressEntries pe
                INNER JOIN ChallengeActivities a ON pe.ChallengeActivityId = a.Id
                INNER JOIN ChallengeActivityGoal gl ON a.Id = gl.ChallengeActivityId
                INNER JOIN ChallengeGoals g ON gl.ChallengeGoalId = g.Id
                WHERE gl.ChallengeGoalId = GoalProgresses.ChallengeGoalId
                  AND pe.UserId = GoalProgresses.UserId
                  AND (
                       (a.ActivityType = g.MetricCategory)
                       OR (a.ActivityType = 'DistanceAndTime' AND g.MetricCategory IN ('Distance', 'Time'))
                       OR (a.ActivityType = 'Occurrence' AND g.MetricCategory = 'Count')
                  )
            )
            WHERE EXISTS (
                SELECT 1 FROM ChallengeActivityGoal gl
                WHERE gl.ChallengeGoalId = GoalProgresses.ChallengeGoalId
            )
        ");
    }
    catch { /* Table may not have data yet */ }

    // Recalculate per-entry goals (use MAX instead of SUM)
    try
    {
        db.Database.ExecuteSqlRaw(@"
            UPDATE GoalProgresses
            SET CurrentValue = (
                SELECT COALESCE(MAX(
                    CASE
                        WHEN a.ActivityType = 'DistanceAndTime' AND g.MetricCategory = 'Time'
                            THEN COALESCE(pe.TimeAmount, 0) * a.PointValue
                        ELSE pe.Amount * a.PointValue
                    END
                ), 0)
                FROM ProgressEntries pe
                INNER JOIN ChallengeActivities a ON pe.ChallengeActivityId = a.Id
                INNER JOIN ChallengeActivityGoal gl ON a.Id = gl.ChallengeActivityId
                INNER JOIN ChallengeGoals g ON gl.ChallengeGoalId = g.Id
                WHERE gl.ChallengeGoalId = GoalProgresses.ChallengeGoalId
                  AND pe.UserId = GoalProgresses.UserId
                  AND (
                       (a.ActivityType = g.MetricCategory)
                       OR (a.ActivityType = 'DistanceAndTime' AND g.MetricCategory IN ('Distance', 'Time'))
                       OR (a.ActivityType = 'Occurrence' AND g.MetricCategory = 'Count')
                  )
            )
            WHERE EXISTS (
                SELECT 1 FROM ChallengeGoals g2
                WHERE g2.Id = GoalProgresses.ChallengeGoalId AND g2.IsPerEntry = 1
            )
        ");
    }
    catch { /* Table may not have data yet */ }

    // Re-evaluate IsCompleted/CompletedAt after recalculating
    try
    {
        db.Database.ExecuteSqlRaw(@"
            UPDATE GoalProgresses
            SET IsCompleted = CASE
                    WHEN g.TargetValue IS NOT NULL AND GoalProgresses.CurrentValue >= g.TargetValue THEN 1
                    ELSE 0
                END,
                CompletedAt = CASE
                    WHEN g.TargetValue IS NOT NULL AND GoalProgresses.CurrentValue >= g.TargetValue
                         AND GoalProgresses.CompletedAt IS NULL THEN datetime('now')
                    ELSE GoalProgresses.CompletedAt
                END
            FROM ChallengeGoals g
            WHERE g.Id = GoalProgresses.ChallengeGoalId
              AND g.Type IN ('Achievement', 'Streak')
        ");
    }
    catch { /* Table may not have data yet */ }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    var frontendPath = Path.Combine(app.Environment.ContentRootPath, "..", "..", "frontend");
    frontendPath = Path.GetFullPath(frontendPath);
    var fileProvider = new PhysicalFileProvider(frontendPath);

    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapFamilyEndpoints();
app.MapChallengeEndpoints();
app.MapGoalEndpoints();
app.MapRedeemEndpoints();

app.Run();

public partial class Program { }