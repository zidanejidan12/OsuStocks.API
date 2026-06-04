using OsuStocks.Domain.OsuIntegration.Models;

namespace OsuStocks.Domain.OsuIntegration.Interfaces;

public interface IOsuApiClient
{
    Task<OsuUserProfile> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<OsuUserProfile> GetUserAsync(long osuUserId, string accessToken, CancellationToken cancellationToken = default);
}
