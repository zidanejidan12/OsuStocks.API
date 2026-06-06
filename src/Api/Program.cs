using Hangfire;
using Hangfire.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OsuStocks.Api.Common;
using OsuStocks.Api.Middleware;
using OsuStocks.Api.Security;
using OsuStocks.Application;
using OsuStocks.Application.Features.OsuIntegration.Auth.GetCurrentUserProfile;
using OsuStocks.Application.Features.OsuIntegration.Auth.GetOsuLoginUrl;
using OsuStocks.Application.Features.OsuIntegration.Auth.HandleOsuCallback;
using OsuStocks.Application.Features.PlayerRegistry.AddTrackedPlayer;
using OsuStocks.Application.Features.PlayerRegistry.DisableTrackedPlayer;
using OsuStocks.Application.Features.PlayerRegistry.EnableTrackedPlayer;
using OsuStocks.Application.Features.PlayerRegistry.ListTrackedPlayers;
using OsuStocks.Application.Features.PlayerRegistry.SearchOsuPlayers;
using OsuStocks.Application.Features.Market.GetMarketOverview;
using OsuStocks.Application.Features.Market.GetMarketStocks;
using OsuStocks.Application.Features.Market.GetMarketStockDetails;
using OsuStocks.Application.Features.Market.GetMarketStockHistory;
using OsuStocks.Application.Features.Trading.BuyStock;
using OsuStocks.Application.Features.Trading.GetHoldings;
using OsuStocks.Application.Features.Trading.GetTradeHistory;
using OsuStocks.Application.Features.Portfolio.GetPortfolioSummary;
using OsuStocks.Application.Features.Wallet.GetWallet;
using OsuStocks.Application.Features.Wallet.GetWalletTransactions;
using OsuStocks.Application.Features.Admin.MarketSettings.GetMarketSettings;
using OsuStocks.Application.Features.Admin.MarketSettings.UpdateMarketSettings;
using OsuStocks.Application.Features.Trading.SellStock;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Infrastructure;
using OsuStocks.Infrastructure.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var swaggerEnabled = builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Security:EnableSwagger");

ValidateProductionSecretEnvironmentVariables(builder.Configuration, builder.Environment);

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("trading", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });
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
        tags: ["cache", "ready"]);

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

app.MapHealthChecks("/health", healthCheckOptions);
app.MapHealthChecks("/api/v1/health", healthCheckOptions);

var authGroup = app.MapGroup("/api/v1/auth")
    .RequireRateLimiting("auth");

authGroup.MapGet("/login", async (
    string? returnUrl,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetOsuLoginUrlQuery(returnUrl), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Redirect(result.Value.AuthorizationUrl);
});

authGroup.MapGet("/callback", async (
    string code,
    string state,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new HandleOsuCallbackCommand(code, state), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new
    {
        accessToken = result.Value.AccessToken,
        expiresAt = result.Value.ExpiresAt,
        returnUrl = result.Value.ReturnUrl
    });
});

authGroup.MapGet("/me", async (
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!TryResolveUserId(principal, out var userId))
    {
        return Results.Json(new
        {
            code = "UNAUTHORIZED",
            message = "Authentication token is missing a valid user identifier.",
            traceId = httpContext.TraceIdentifier
        }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var result = await sender.Send(new GetCurrentUserProfileQuery(userId), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new
    {
        userId = result.Value.UserId,
        osuUserId = result.Value.OsuUserId,
        username = result.Value.Username,
        role = result.Value.Role
    });
})
.RequireAuthorization();

var marketGroup = app.MapGroup("/api/v1/market")
    .RequireAuthorization();

marketGroup.MapGet("", async (
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetMarketOverviewQuery(), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    Dictionary<string, object?> ToMoverObject(OsuStocks.Application.Features.Market.GetMarketOverview.MarketTopMoverResponse? mover)
    {
        if (mover is null)
        {
            return new Dictionary<string, object?>();
        }

        return new Dictionary<string, object?>
        {
            ["stockId"] = mover.StockId,
            ["playerName"] = mover.PlayerName,
            ["currentPrice"] = mover.CurrentPrice,
            ["priceChange24h"] = mover.PriceChange24h
        };
    }

    return Results.Ok(new
    {
        totalStocks = result.Value.TotalStocks,
        totalVolume = result.Value.TotalVolume,
        topGainer = ToMoverObject(result.Value.TopGainer),
        topLoser = ToMoverObject(result.Value.TopLoser)
    });
});

marketGroup.MapGet("/stocks", async (
    int? page,
    int? pageSize,
    string? sort,
    string? search,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetMarketStocksQuery(
        page ?? 1,
        pageSize ?? 25,
        sort,
        search), cancellationToken);

    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new
    {
        items = result.Value.Items,
        page = result.Value.Page,
        pageSize = result.Value.PageSize,
        totalCount = result.Value.TotalCount
    });
});

marketGroup.MapGet("/stocks/{stockId:guid}", async (
    Guid stockId,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetMarketStockDetailsQuery(stockId), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new
    {
        stockId = result.Value.StockId,
        playerName = result.Value.PlayerName,
        currentPrice = result.Value.CurrentPrice,
        volume = result.Value.Volume,
        priceChange24h = result.Value.PriceChange24h
    });
});

marketGroup.MapGet("/stocks/{stockId:guid}/history", async (
    Guid stockId,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetMarketStockHistoryQuery(stockId), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(result.Value.Items.Select(x => new
    {
        timestamp = x.Timestamp,
        price = x.Price
    }));
});
var tradingGroup = app.MapGroup("/api/v1/trading")
    .RequireAuthorization()
    .RequireRateLimiting("trading");

tradingGroup.MapPost("/buy", async (
    TradeStockRequest request,
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!TryResolveUserId(principal, out var userId))
    {
        return UnauthorizedResult(httpContext);
    }

    var result = await sender.Send(new BuyStockCommand(userId, request.StockId, request.Quantity), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new
    {
        tradeId = result.Value.TradeId,
        unitPrice = result.Value.UnitPrice,
        totalAmount = result.Value.TotalAmount,
        status = "Completed"
    });
});

tradingGroup.MapPost("/sell", async (
    TradeStockRequest request,
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!TryResolveUserId(principal, out var userId))
    {
        return UnauthorizedResult(httpContext);
    }

    var result = await sender.Send(new SellStockCommand(userId, request.StockId, request.Quantity), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new
    {
        tradeId = result.Value.TradeId,
        unitPrice = result.Value.UnitPrice,
        totalAmount = result.Value.TotalAmount,
        status = "Completed"
    });
});

tradingGroup.MapGet("/history", async (
    int? page,
    int? pageSize,
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!TryResolveUserId(principal, out var userId))
    {
        return UnauthorizedResult(httpContext);
    }

    var result = await sender.Send(new GetTradeHistoryQuery(userId, page ?? 1, pageSize ?? 25), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new { items = result.Value.Items });
});

var portfolioGroup = app.MapGroup("/api/v1/portfolio")
    .RequireAuthorization();

portfolioGroup.MapGet("", async (
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!TryResolveUserId(principal, out var userId))
    {
        return UnauthorizedResult(httpContext);
    }

    var result = await sender.Send(new GetPortfolioSummaryQuery(userId), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new
    {
        currentValue = result.Value.CurrentValue,
        costBasis = result.Value.CostBasis,
        profitLoss = result.Value.ProfitLoss,
        holdings = result.Value.Holdings
    });
});

portfolioGroup.MapGet("/holdings", async (
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!TryResolveUserId(principal, out var userId))
    {
        return UnauthorizedResult(httpContext);
    }

    var result = await sender.Send(new GetHoldingsQuery(userId), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new { items = result.Value.Items });
});

var walletGroup = app.MapGroup("/api/v1/wallet")
    .RequireAuthorization();

walletGroup.MapGet("", async (
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!TryResolveUserId(principal, out var userId))
    {
        return UnauthorizedResult(httpContext);
    }

    var result = await sender.Send(new GetWalletQuery(userId), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new { balance = result.Value.Balance });
});

walletGroup.MapGet("/transactions", async (
    int? page,
    int? pageSize,
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!TryResolveUserId(principal, out var userId))
    {
        return UnauthorizedResult(httpContext);
    }

    var result = await sender.Send(new GetWalletTransactionsQuery(userId, page ?? 1, pageSize ?? 25), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new { items = result.Value.Items });
});
var adminGroup = app.MapGroup("/api/v1/admin")
    .RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()));

adminGroup.MapGet("/market-settings", async (
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetMarketSettingsQuery(), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new
    {
        ppMultiplier = result.Value.PpMultiplier,
        tradeMultiplier = result.Value.TradeMultiplier,
        decayMultiplier = result.Value.DecayMultiplier,
        isMaintenanceMode = result.Value.IsMaintenanceMode
    });
});

adminGroup.MapPut("/market-settings", async (
    UpdateMarketSettingsRequest request,
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var actor = ResolveActor(principal);
    var result = await sender.Send(
        new UpdateMarketSettingsCommand(
            request.PpMultiplier,
            request.TradeMultiplier,
            request.DecayMultiplier,
            request.IsMaintenanceMode,
            actor),
        cancellationToken);

    if (!result.IsSuccess)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.NoContent();
});

var trackedPlayersGroup = adminGroup.MapGroup("/tracked-players");

trackedPlayersGroup.MapGet("", async (
    bool? isActive,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new ListTrackedPlayersQuery(isActive), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new { items = result.Value.Items });
});

trackedPlayersGroup.MapGet("/search", async (
    string query,
    int? limit,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new SearchOsuPlayersQuery(query, limit ?? 10), cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new { items = result.Value.Items });
});

trackedPlayersGroup.MapPost("", async (
    AddTrackedPlayerRequest request,
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var actor = ResolveActor(principal);
    var result = await sender.Send(
        new AddTrackedPlayerCommand(request.OsuUserId, request.TrackingTier, actor),
        cancellationToken);

    if (!result.IsSuccess || result.Value is null)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.Ok(new { trackedPlayerId = result.Value.TrackedPlayerId });
});

trackedPlayersGroup.MapPatch("/{id:guid}/enable", async (
    Guid id,
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var actor = ResolveActor(principal);
    var result = await sender.Send(new EnableTrackedPlayerCommand(id, actor), cancellationToken);

    if (!result.IsSuccess)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.NoContent();
});

trackedPlayersGroup.MapPatch("/{id:guid}/disable", async (
    Guid id,
    ClaimsPrincipal principal,
    ISender sender,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var actor = ResolveActor(principal);
    var result = await sender.Send(new DisableTrackedPlayerCommand(id, actor), cancellationToken);

    if (!result.IsSuccess)
    {
        return result.Error!.ToErrorResult(httpContext);
    }

    return Results.NoContent();
});

var hangfireDashboardAuthorizationFilter = app.Services.GetRequiredService<IDashboardAuthorizationFilter>();

app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [hangfireDashboardAuthorizationFilter]
})
.RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()));

app.Run();

static bool TryResolveUserId(ClaimsPrincipal principal, out Guid userId)
{
    var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(userIdValue, out userId);
}

static Microsoft.AspNetCore.Http.IResult UnauthorizedResult(HttpContext httpContext)
{
    return Results.Json(new
    {
        code = "UNAUTHORIZED",
        message = "Authentication token is missing a valid user identifier.",
        traceId = httpContext.TraceIdentifier
    }, statusCode: StatusCodes.Status401Unauthorized);
}

static string? ResolveActor(ClaimsPrincipal principal)
{
    return principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue(ClaimTypes.Name)
        ?? principal.Identity?.Name;
}

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

public sealed record AddTrackedPlayerRequest(long OsuUserId, TrackingTier TrackingTier = TrackingTier.Tier3);
public sealed record UpdateMarketSettingsRequest(decimal PpMultiplier, decimal TradeMultiplier, decimal DecayMultiplier, bool IsMaintenanceMode);
public sealed record TradeStockRequest(Guid StockId, int Quantity);

public partial class Program
{
}



















