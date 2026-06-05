using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class OAuthCallbackEndpointsTests
{
    private const string JwtIssuer = "osu-stocks-test";
    private const string JwtAudience = "osu-stocks-test-client";
    private const string JwtSigningKey = "test-signing-key-that-is-at-least-32-characters-long";

    [Fact]
    public async Task Callback_CreatesUserWhenMissing_AndReturnsJwt()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<InMemoryUserRepository>();
        var walletRepository = scope.ServiceProvider.GetRequiredService<InMemoryWalletRepository>();
        var walletTransactionRepository = scope.ServiceProvider.GetRequiredService<InMemoryWalletTransactionRepository>();
        var portfolioRepository = scope.ServiceProvider.GetRequiredService<InMemoryPortfolioRepository>();
        var tokenManager = scope.ServiceProvider.GetRequiredService<IOsuTokenManager>();

        const string state = "oauth-state-create-user";
        const string returnUrl = "http://localhost:3000/dashboard";
        await tokenManager.StoreAuthorizationStateAsync(state, returnUrl, TimeSpan.FromMinutes(5));

        var response = await client.GetAsync($"/api/v1/auth/callback?code=valid-code&state={state}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CallbackResponse>();

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.Equal(returnUrl, payload.ReturnUrl);

        var user = await userRepository.GetByOsuUserIdAsync(1001);

        Assert.NotNull(user);
        Assert.Equal(1, userRepository.Count);

        var wallet = await walletRepository.GetByUserIdAsync(user.Id);

        Assert.NotNull(wallet);
        Assert.Equal(1, walletRepository.Count);
        Assert.Equal(100_000m, wallet.Balance);

        var transactions = await walletTransactionRepository.GetByWalletIdAsync(wallet.Id, 0, 10);

        Assert.Single(transactions);
        Assert.Equal(WalletTransactionType.InitialGrant, transactions[0].TransactionType);
        Assert.Equal(100_000m, transactions[0].Amount);

        var portfolio = await portfolioRepository.GetByUserIdAsync(user.Id);

        Assert.NotNull(portfolio);
        Assert.Equal(1, portfolioRepository.Count);

        var principal = ValidateJwt(payload.AccessToken);

        Assert.Equal("1001", principal.FindFirst("osu_user_id")?.Value);
        Assert.Equal(user.Id.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        var savedToken = await tokenManager.GetUserTokenAsync(user.Id);

        Assert.NotNull(savedToken);
        Assert.Equal("oauth-user-token", savedToken.AccessToken);
    }

    [Fact]
    public async Task Callback_ReusesExistingUser_AndReturnsJwt()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<InMemoryUserRepository>();
        var walletRepository = scope.ServiceProvider.GetRequiredService<InMemoryWalletRepository>();
        var walletTransactionRepository = scope.ServiceProvider.GetRequiredService<InMemoryWalletTransactionRepository>();
        var portfolioRepository = scope.ServiceProvider.GetRequiredService<InMemoryPortfolioRepository>();
        var tokenManager = scope.ServiceProvider.GetRequiredService<IOsuTokenManager>();

        var existingUserId = Guid.NewGuid();
        await userRepository.AddAsync(new User
        {
            Id = existingUserId,
            OsuUserId = 1001,
            Username = "old-username",
            AvatarUrl = "https://avatar.example/old",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
            CreatedBy = "seed",
            LastLoginAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        const string state = "oauth-state-reuse-user";
        await tokenManager.StoreAuthorizationStateAsync(state, null, TimeSpan.FromMinutes(5));

        var response = await client.GetAsync($"/api/v1/auth/callback?code=valid-code&state={state}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CallbackResponse>();

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));

        var user = await userRepository.GetByOsuUserIdAsync(1001);

        Assert.NotNull(user);
        Assert.Equal(existingUserId, user.Id);
        Assert.Equal("mrekk", user.Username);
        Assert.Equal("https://avatar.example/mrekk", user.AvatarUrl);
        Assert.Equal(1, userRepository.Count);

        Assert.Equal(0, walletRepository.Count);
        Assert.Equal(0, walletTransactionRepository.Count);
        Assert.Equal(0, portfolioRepository.Count);

        var principal = ValidateJwt(payload.AccessToken);

        Assert.Equal("1001", principal.FindFirst("osu_user_id")?.Value);
        Assert.Equal(existingUserId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        var savedToken = await tokenManager.GetUserTokenAsync(existingUserId);

        Assert.NotNull(savedToken);
        Assert.Equal("oauth-user-token", savedToken.AccessToken);
    }

    private static ClaimsPrincipal ValidateJwt(string accessToken)
    {
        var handler = new JwtSecurityTokenHandler();

        return handler.ValidateToken(
            accessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = JwtIssuer,
                ValidateAudience = true,
                ValidAudience = JwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            },
            out _);
    }

    private sealed record CallbackResponse(
        [property: JsonPropertyName("accessToken")] string AccessToken,
        [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt,
        [property: JsonPropertyName("returnUrl")] string? ReturnUrl);
}
