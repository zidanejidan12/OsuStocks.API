using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class MarketEventRepository(AppDbContext dbContext) : IMarketEventRepository
{
    public Task AddAsync(MarketEvent marketEvent, CancellationToken cancellationToken = default)
    {
        return dbContext.MarketEvents.AddAsync(marketEvent, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<MarketEvent>> GetLatestByStockIdAsync(
        Guid stockId,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.MarketEvents
            .AsNoTracking()
            .Where(x => x.StockId == stockId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        // Set-based delete to bound table growth. Market events older than the retention window are
        // only used for the activity feed / recent top plays, so they're safe to drop.
        return dbContext.MarketEvents
            .Where(x => x.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
