using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IMarketEventRepository
{
    Task AddAsync(MarketEvent marketEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketEvent>> GetLatestByStockIdAsync(
        Guid stockId,
        int take,
        CancellationToken cancellationToken = default);
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}
