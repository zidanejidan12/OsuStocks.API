using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OsuStocks.Api.Endpoints;
using OsuStocks.Api.Middleware;
using OsuStocks.Api.Security;
using OsuStocks.Application;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Infrastructure;
using OsuStocks.Infrastructure.Authentication;
using OsuStocks.Infrastructure.OsuIntegration.Telemetry;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var swaggerEnabled = builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Security:EnableSwagger");

ValidateProductionSecretEnvironmentVariables(builder.Configuration, builder.Environment);

builder.Services.AddCors(options =>
{
    // Read allowed origins when CORS options are configured (after the host builder has run) rather
    // than eagerly during startup, so configuration layered on afterwards — e.g. WebApplicationFactory
    // overrides in integration tests — is honored. In production this resolves the same appsettings values.
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    // Fail closed and loud: outside Development an empty origin allow-list would otherwise silently
    // reject every browser, which is hard to diagnose. Require an explicit allow-list in production.
    if (!builder.Environment.IsDevelopment() && corsOrigins.Length == 0)
    {
        throw new InvalidOperationException(
            "Cors:AllowedOrigins must be configured with at least one origin outside Development.");
    }

    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .WithHeaders("Content-Type", "Authorization")
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
              .AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global per-IP backstop across EVERY endpoint. The named "auth"/"trading" policies below are
    // stricter and stack on top of this for their endpoints; this catches everything else (market
    // data, leaderboard, portfolio, etc.) that would otherwise be unthrottled. 300/min ≈ 5 req/s
    // per IP — generous for a real SPA session, but stops a single host from hammering the box.
    // It does NOT stop a distributed botnet (that needs an edge layer like Cloudflare).
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Partition by client IP so one visitor's traffic can never exhaust the budget for everyone.
    // A non-partitioned limiter is a single shared bucket across all callers, which lets a handful
    // of concurrent logins lock out the whole site.
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.AddPolicy("trading", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddNpgSql(
        sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")!,
        name: "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "ready"])
    .AddRedis(
        sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Redis")!,
        name: "redis",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["cache", "ready"])
    .AddCheck<OsuApiHealthCheck>(
        name: "osu-api",
        failureStatus: HealthStatus.Degraded,
        tags: ["external", "live"]);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be configured and contain at least 32 characters.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// Accept/emit enums as their string names so request binding matches the
// string enum values the API already returns in responses (e.g. "Tier3").
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<IDashboardAuthorizationFilter, HangfireDashboardAuthorizationFilter>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OsuStocks API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter: Bearer {your JWT token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        BearerFormat = "JWT"
    });
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "OsuStocks API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

var healthCheckOptions = new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
};

// Health checks are polled by the Docker healthcheck + external uptime monitors; exempt them from
// the global rate limiter so probes can't be throttled (and a 429 misreported as "down").
app.MapHealthChecks("/health", healthCheckOptions).DisableRateLimiting();
app.MapHealthChecks("/api/v1/health", healthCheckOptions).DisableRateLimiting();

// Feature endpoints live in src/Api/Endpoints/*Endpoints.cs as MapXEndpoints() extension methods.
app.MapAuthEndpoints();
app.MapMarketEndpoints();
app.MapLeaderboardEndpoints();
app.MapTradingEndpoints();
app.MapPortfolioEndpoints();
app.MapWalletEndpoints();
app.MapNotificationEndpoints();
app.MapInvestorEndpoints();
app.MapAchievementEndpoints();
app.MapMissionEndpoints();
app.MapProfileEndpoints();
app.MapAdminEndpoints();
app.MapDailyLoginEndpoints();

var hangfireDashboardAuthorizationFilter = app.Services.GetRequiredService<IDashboardAuthorizationFilter>();

app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [hangfireDashboardAuthorizationFilter]
})
.RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()));

app.Run();

static void ValidateProductionSecretEnvironmentVariables(IConfiguration configuration, IHostEnvironment environment)
{
    if (!environment.IsProduction())
    {
        return;
    }

    string[] requiredEnvironmentVariables =
    [
        "ConnectionStrings__Postgres",
        "ConnectionStrings__Redis",
        "OsuOAuth__ClientId",
        "OsuOAuth__ClientSecret",
        "OsuOAuth__RedirectUri",
        "Jwt__Issuer",
        "Jwt__Audience",
        "Jwt__SigningKey"
    ];

    var missingVariables = requiredEnvironmentVariables
        .Where(static key => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        .ToArray();

    if (missingVariables.Length > 0)
    {
        throw new InvalidOperationException(
            "Production requires secrets via environment variables. Missing: " + string.Join(", ", missingVariables));
    }

    EnsureNotPlaceholder("ConnectionStrings:Postgres", configuration.GetConnectionString("Postgres"));
    EnsureNotPlaceholder("OsuOAuth:ClientSecret", configuration["OsuOAuth:ClientSecret"]);
    EnsureNotPlaceholder("Jwt:SigningKey", configuration["Jwt:SigningKey"]);
}

static void EnsureNotPlaceholder(string key, string? value)
{
    if (string.IsNullOrWhiteSpace(value) || value.Contains("replace-with-", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Configuration '{key}' must be a non-placeholder value in production.");
    }
}

// Resolve the real client IP for rate-limit partitioning. In production the API sits behind a
// single Caddy hop, which *appends* the observed peer to X-Forwarded-For. The rightmost entry is
// therefore the IP Caddy actually saw; the leftmost is client-supplied and spoofable, so a caller
// can't vary it to dodge the limiter. Falls back to the socket address for direct/local calls.
// A stable non-empty key is always returned so anonymous callers are still grouped per-IP rather
// than collapsed into one shared partition.
static string GetClientIp(HttpContext httpContext)
{
    var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
    if (!string.IsNullOrWhiteSpace(forwardedFor))
    {
        var parts = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 0)
        {
            return parts[^1];
        }
    }

    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public partial class Program
{
}
