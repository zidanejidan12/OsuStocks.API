using OsuStocks.Domain.OsuIntegration.Models;

namespace OsuStocks.Domain.OsuIntegration.Interfaces;

public interface IOsuOAuthService
{
    string BuildAuthorizationUrl(string state);

    Task<OsuOAuthToken> ExchangeCodeForTokenAsync(string code, CancellationToken cancellationToken = default);

    Task<OsuOAuthToken> GetClientCredentialsTokenAsync(CancellationToken cancellationToken = default);
}
