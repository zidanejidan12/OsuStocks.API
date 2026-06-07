using OsuStocks.Domain.Models.Leaderboards;

namespace OsuStocks.Domain.Repositories;

public interface ILeaderboardReadRepository
{
    Task<IReadOnlyList<LeaderboardEntryReadModel>> GetWealthAsync(
        DateTimeOffset periodStart,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaderboardEntryReadModel>> GetProfitAsync(
        DateTimeOffset periodStart,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaderboardEntryReadModel>> GetTradersAsync(
        DateTimeOffset periodStart,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
