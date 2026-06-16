using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IStockPriceHistoryRepository
{
    Task AddAsync(StockPriceHistory historyEntry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockPriceHistory>> GetLatestByStockIdAsync(
        Guid stockId,
        int take,
        CancellationToken cancellationToken = default);
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}
