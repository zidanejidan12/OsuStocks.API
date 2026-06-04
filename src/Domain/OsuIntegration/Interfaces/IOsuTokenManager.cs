using OsuStocks.Domain.OsuIntegration.Models;

namespace OsuStocks.Domain.OsuIntegration.Interfaces;

public interface IOsuTokenManager
{
    Task StoreAuthorizationStateAsync(
        string state,
        string? returnUrl,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    Task<OsuAuthorizationState?> ConsumeAuthorizationStateAsync(
        string state,
        CancellationToken cancellationToken = default);

    Task SaveUserTokenAsync(Guid userId, OsuOAuthToken token, CancellationToken cancellationToken = default);

    Task<OsuOAuthToken?> GetUserTokenAsync(Guid userId, CancellationToken cancellationToken = default);
}
