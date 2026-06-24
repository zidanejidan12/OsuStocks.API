using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Distributed;
using OsuStocks.Application.Common.Interfaces;

namespace OsuStocks.Infrastructure.Authentication;

/// <summary>
/// Distributed (Redis-backed) refresh-token store. Only a SHA-256 hash of each token is persisted, so a
/// dump of the cache never yields a usable token. Each token maps to its owning user id and carries a
/// sliding 30-day lifetime that is renewed on every rotation, so an actively-used session effectively
/// never expires while an idle one ages out.
/// </summary>
internal sealed class RedisRefreshTokenService(IDistributedCache cache) : IRefreshTokenService
{
    private const string KeyPrefix = "auth:refresh:";
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public async Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var token = GenerateToken();
        var expiresAt = DateTimeOffset.UtcNow.Add(Lifetime);

        await cache.SetStringAsync(
            BuildKey(token),
            userId.ToString("N"),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime },
            cancellationToken);

        return new RefreshTokenResult(token, expiresAt);
    }

    public async Task<RefreshTokenRotation?> ValidateAndRotateAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var key = BuildKey(refreshToken);
        var stored = await cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(stored) || !Guid.TryParseExact(stored, "N", out var userId))
        {
            return null;
        }

        // Single-use: consume the presented token before minting its replacement so it can't be replayed.
        await cache.RemoveAsync(key, cancellationToken);

        var issued = await IssueAsync(userId, cancellationToken);
        return new RefreshTokenRotation(userId, issued.Token, issued.ExpiresAt);
    }

    private static string GenerateToken()
    {
        // 256 bits of entropy, URL-safe so it survives the callback URL fragment unescaped.
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string BuildKey(string token)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return KeyPrefix + Convert.ToHexString(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
