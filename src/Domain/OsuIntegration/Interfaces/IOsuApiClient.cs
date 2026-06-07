using OsuStocks.Domain.OsuIntegration.Models;

namespace OsuStocks.Domain.OsuIntegration.Interfaces;

public interface IOsuApiClient
{
    Task<OsuUserProfile> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<OsuUserProfile> GetUserAsync(
        long osuUserId,
        string accessToken,
        bool includeTopScore = true,
        CancellationToken cancellationToken = default);

    Task<OsuTopScore?> GetTopScoreAsync(
        long osuUserId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OsuUserProfile>> SearchUsersAsync(
        string query,
        string accessToken,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
