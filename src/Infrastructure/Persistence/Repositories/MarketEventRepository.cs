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
}
