using OsuStocks.Domain.Models;

namespace OsuStocks.Domain.Repositories;

public interface ITradeReadRepository
{
    Task<IReadOnlyList<TradeHistoryReadModel>> GetTradeHistoryByUserIdAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Total shares traded for a stock since <paramref name="since"/> (recent-volume input to liquidity).</summary>
    Task<decimal> GetSharesTradedSinceAsync(
        Guid stockId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
}
