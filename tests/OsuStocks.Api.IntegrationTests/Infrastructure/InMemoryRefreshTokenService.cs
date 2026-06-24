using System.Collections.Concurrent;
using OsuStocks.Application.Common.Interfaces;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryRefreshTokenService : IRefreshTokenService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);
    private readonly ConcurrentDictionary<string, Guid> _tokens = new();

    public Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        _tokens[token] = userId;
        return Task.FromResult(new RefreshTokenResult(token, DateTimeOffset.UtcNow.Add(Lifetime)));
    }

    public Task<RefreshTokenRotation?> ValidateAndRotateAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || !_tokens.TryRemove(refreshToken, out var userId))
        {
            return Task.FromResult<RefreshTokenRotation?>(null);
        }

        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        _tokens[token] = userId;
        return Task.FromResult<RefreshTokenRotation?>(
            new RefreshTokenRotation(userId, token, DateTimeOffset.UtcNow.Add(Lifetime)));
    }
}
