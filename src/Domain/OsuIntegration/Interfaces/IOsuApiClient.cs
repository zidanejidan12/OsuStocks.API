using OsuStocks.Domain.OsuIntegration.Models;

namespace OsuStocks.Domain.OsuIntegration.Interfaces;

public interface IOsuApiClient
{
    Task<OsuUserProfile> GetCurrentUserAsync(
        string accessToken,
        bool includeTopScore = true,
        CancellationToken cancellationToken = default);

    Task<OsuUserProfile> GetUserAsync(
        long osuUserId,
        string accessToken,
        bool includeTopScore = true,
        CancellationToken cancellationToken = default);

    Task<OsuTopScore?> GetTopScoreAsync(
        long osuUserId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OsuTopScore>> GetTopScoresAsync(
        long osuUserId,
        string accessToken,
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OsuUserProfile>> SearchUsersAsync(
        string query,
        string accessToken,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page (50 entries) of the global osu! standard performance ranking, ordered by rank.
    /// osu! caps this leaderboard at page 200 (the top 10,000). Used to bulk-seed tracked players.
    /// </summary>
    Task<IReadOnlyList<OsuUserProfile>> GetPerformanceRankingsAsync(
        int page,
        string accessToken,
        CancellationToken cancellationToken = default);
}
