using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryOsuTokenManager : IOsuTokenManager
{
    private readonly ConcurrentDictionary<string, AuthorizationStateEntry> _states = new();
    private readonly ConcurrentDictionary<Guid, OsuOAuthToken> _userTokens = new();

    public Task StoreAuthorizationStateAsync(
        string state,
        string? returnUrl,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : lifetime);
        _states[state] = new AuthorizationStateEntry(returnUrl, expiresAt);
        return Task.CompletedTask;
    }

    public Task<OsuAuthorizationState?> ConsumeAuthorizationStateAsync(
        string state,
        CancellationToken cancellationToken = default)
    {
        if (!_states.TryRemove(state, out var entry))
        {
            return Task.FromResult<OsuAuthorizationState?>(null);
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Task.FromResult<OsuAuthorizationState?>(null);
        }

        return Task.FromResult<OsuAuthorizationState?>(new OsuAuthorizationState(state, entry.ReturnUrl));
    }

    public Task SaveUserTokenAsync(Guid userId, OsuOAuthToken token, CancellationToken cancellationToken = default)
    {
        _userTokens[userId] = token;
        return Task.CompletedTask;
    }

    public Task<OsuOAuthToken?> GetUserTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _userTokens.TryGetValue(userId, out var token);
        return Task.FromResult(token);
    }

    private sealed record AuthorizationStateEntry(string? ReturnUrl, DateTimeOffset ExpiresAt);
}
