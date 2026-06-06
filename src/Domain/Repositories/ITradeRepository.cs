using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface ITradeRepository
{
    Task AddAsync(Trade trade, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trade>> GetByUserIdAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
    Task<Trade?> GetLastByUserAndStockAsync(
        Guid userId,
        Guid stockId,
        CancellationToken cancellationToken = default);
    Task<int> CountRecentByUserAsync(
        Guid userId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
}
