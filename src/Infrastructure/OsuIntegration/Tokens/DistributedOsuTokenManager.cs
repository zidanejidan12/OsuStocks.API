using Microsoft.Extensions.Caching.Distributed;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;
using System.Text.Json;

namespace OsuStocks.Infrastructure.OsuIntegration.Tokens;

internal sealed class DistributedOsuTokenManager(IDistributedCache cache) : IOsuTokenManager
{
    private const string UserTokenKeyPrefix = "osu:user-token:";
    private const string AuthStateKeyPrefix = "osu:auth-state:";

    public async Task StoreAuthorizationStateAsync(
        string state,
        string? returnUrl,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var key = BuildAuthStateKey(state);
        var cacheValue = JsonSerializer.Serialize(new AuthorizationStateCacheItem(returnUrl));

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = lifetime
        };

        await cache.SetStringAsync(key, cacheValue, options, cancellationToken);
    }

    public async Task<OsuAuthorizationState?> ConsumeAuthorizationStateAsync(
        string state,
        CancellationToken cancellationToken = default)
    {
        var key = BuildAuthStateKey(state);
        var cacheValue = await cache.GetStringAsync(key, cancellationToken);
        if (cacheValue is null)
        {
            return null;
        }

        await cache.RemoveAsync(key, cancellationToken);

        var item = JsonSerializer.Deserialize<AuthorizationStateCacheItem>(cacheValue)
            ?? new AuthorizationStateCacheItem(null);

        return new OsuAuthorizationState(state, item.ReturnUrl);
    }

    public async Task SaveUserTokenAsync(
        Guid userId,
        OsuOAuthToken token,
        CancellationToken cancellationToken = default)
    {
        var key = BuildUserTokenKey(userId);
        var payload = JsonSerializer.Serialize(token);

        var expiresIn = token.ExpiresAt - DateTimeOffset.UtcNow;
        if (expiresIn <= TimeSpan.Zero)
        {
            expiresIn = TimeSpan.FromMinutes(1);
        }

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiresIn
        };

        await cache.SetStringAsync(key, payload, options, cancellationToken);
    }

    public async Task<OsuOAuthToken?> GetUserTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var payload = await cache.GetStringAsync(BuildUserTokenKey(userId), cancellationToken);
        if (payload is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<OsuOAuthToken>(payload);
    }

    private static string BuildUserTokenKey(Guid userId)
    {
        return $"{UserTokenKeyPrefix}{userId:N}";
    }

    private static string BuildAuthStateKey(string state)
    {
        return $"{AuthStateKeyPrefix}{state}";
    }

    private sealed record AuthorizationStateCacheItem(string? ReturnUrl);
}
