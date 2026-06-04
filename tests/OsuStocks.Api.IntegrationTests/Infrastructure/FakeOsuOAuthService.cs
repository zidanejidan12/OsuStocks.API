using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class FakeOsuOAuthService : IOsuOAuthService
{
    public string BuildAuthorizationUrl(string state)
    {
        return $"https://osu.ppy.sh/oauth/authorize?state={state}";
    }

    public Task<OsuOAuthToken> ExchangeCodeForTokenAsync(string code, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new OsuOAuthToken(
            "oauth-user-token",
            "oauth-user-refresh",
            DateTimeOffset.UtcNow.AddHours(1),
            "public identify"));
    }

    public Task<OsuOAuthToken> GetClientCredentialsTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new OsuOAuthToken(
            "oauth-client-token",
            null,
            DateTimeOffset.UtcNow.AddHours(1),
            "public identify"));
    }
}
