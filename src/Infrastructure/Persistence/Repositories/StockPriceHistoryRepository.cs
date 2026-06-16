using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class StockPriceHistoryRepository(AppDbContext dbContext) : IStockPriceHistoryRepository
{
    public Task AddAsync(StockPriceHistory historyEntry, CancellationToken cancellationToken = default)
    {
        return dbContext.StockPriceHistory.AddAsync(historyEntry, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<StockPriceHistory>> GetLatestByStockIdAsync(
        Guid stockId,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.StockPriceHistory
            .AsNoTracking()
            .Where(x => x.StockId == stockId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        // Set-based delete. Current price lives on the stock; rows beyond the retention window are
        // only kept for charts/history, so older rows are safe to drop to bound table growth.
        return dbContext.StockPriceHistory
            .Where(x => x.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
