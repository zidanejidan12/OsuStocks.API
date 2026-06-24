using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class RefreshTokenEndpointsTests
{
    private const string JwtIssuer = "osu-stocks-test";
    private const string JwtAudience = "osu-stocks-test-client";
    private const string JwtSigningKey = "test-signing-key-that-is-at-least-32-characters-long";

    [Fact]
    public async Task Refresh_WithValidToken_RotatesTokenAndMintsJwt()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<InMemoryUserRepository>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        var userId = Guid.NewGuid();
        await userRepository.AddAsync(new User
        {
            Id = userId,
            OsuUserId = 4242,
            Username = "refresher",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed",
            LastLoginAt = DateTimeOffset.UtcNow
        });

        var issued = await refreshTokenService.IssueAsync(userId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = issued.Token });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RefreshResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        // Rotation: a fresh refresh token is returned, not the one we presented.
        Assert.NotEqual(issued.Token, payload.RefreshToken);

        var principal = ValidateJwt(payload.AccessToken);
        Assert.Equal("4242", principal.FindFirst("osu_user_id")?.Value);
        Assert.Equal(userId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        // The presented token is single-use: replaying it is now rejected.
        var reuse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = issued.Token });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // The rotated token works.
        var second = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = payload.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ReturnsUnauthorized()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    private sealed record RefreshResponse(
        [property: JsonPropertyName("accessToken")] string AccessToken,
        [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt,
        [property: JsonPropertyName("refreshToken")] string RefreshToken,
        [property: JsonPropertyName("refreshExpiresAt")] DateTimeOffset RefreshExpiresAt);
}
