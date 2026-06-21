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
    var goalsNeedingCategory = await db.ChallengeGoals
        .Include(g => g.ActivityLinks)
            .ThenInclude(gl => gl.Activity)
        .Where(g => g.MetricCategory == null || g.MetricCategory == "Count")
        .ToListAsync();
    if (goalsNeedingCategory.Count > 0)
    {
        foreach (var goal in goalsNeedingCategory)
        {
            if (goal.ActivityLinks.Any(gl => gl.Activity.ActivityType == "Distance"))
                goal.MetricCategory = "Distance";
            else if (goal.ActivityLinks.Any(gl => gl.Activity.ActivityType == "Time"))
                goal.MetricCategory = "Time";
            else if (goal.ActivityLinks.Any(gl => gl.Activity.ActivityType == "DistanceAndTime"))
                goal.MetricCategory = "Distance";
            else
                goal.MetricCategory = "Count";
        }
        await db.SaveChangesAsync();

        // Recalculate GoalProgress for accumulation and per-entry goals
        var allProgresses = await db.GoalProgresses
            .Include(p => p.Goal)
            .Where(p => p.Goal.Type == "Achievement")
            .ToListAsync();
        foreach (var gp in allProgresses)
        {
            var entries = await db.ProgressEntries
                .Include(e => e.Activity)
                .ThenInclude(a => a.GoalLinks)
                .Where(e => e.Activity.GoalLinks.Any(gl => gl.ChallengeGoalId == gp.ChallengeGoalId)
                    && e.UserId == gp.UserId)
                .ToListAsync();

            if (gp.Goal.IsPerEntry)
            {
                gp.CurrentValue = entries.Max(e => GetMetricDelta(e.Amount, e.TimeAmount, e.Activity, gp.Goal.MetricCategory));
            }
            else
            {
                gp.CurrentValue = entries.Sum(e => GetMetricDelta(e.Amount, e.TimeAmount, e.Activity, gp.Goal.MetricCategory));
            }

            if (gp.Goal.TargetValue.HasValue)
            {
                gp.IsCompleted = gp.CurrentValue >= gp.Goal.TargetValue.Value;
                if (gp.IsCompleted && gp.CompletedAt == null)
                    gp.CompletedAt = DateTime.UtcNow;
            }
            gp.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    static decimal GetMetricDelta(decimal amount, decimal? timeAmount, ChallengeActivity activity, string goalMetricCategory)
    {
        if (activity.ActivityType == "DistanceAndTime")
        {
            if (goalMetricCategory == "Time" && timeAmount.HasValue)
                return timeAmount.Value * activity.PointValue;
            if (goalMetricCategory == "Distance")
                return amount * activity.PointValue;
            return 0;
        }
        var am = activity.ActivityType switch
        {
            "Distance" => "Distance",
            "Time" => "Time",
            _ => "Count"
        };
        return am == goalMetricCategory ? amount * activity.PointValue : 0;
    }
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