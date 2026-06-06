using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class TradeRepository(AppDbContext dbContext) : ITradeRepository
{
    public Task AddAsync(Trade trade, CancellationToken cancellationToken = default)
    {
        return dbContext.Trades.AddAsync(trade, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<Trade>> GetByUserIdAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Trades
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.ExecutedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<Trade?> GetLastByUserAndStockAsync(
        Guid userId,
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Trades
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.StockId == stockId)
            .OrderByDescending(x => x.ExecutedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountRecentByUserAsync(
        Guid userId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Trades
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.ExecutedAt >= since)
            .CountAsync(cancellationToken);
    }
}
