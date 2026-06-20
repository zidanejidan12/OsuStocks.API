using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class TradeReadRepository(AppDbContext dbContext) : ITradeReadRepository
{
    public async Task<IReadOnlyList<TradeHistoryReadModel>> GetTradeHistoryByUserIdAsync(
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
            .Select(x => new TradeHistoryReadModel(
                x.Id,
                x.StockId,
                x.TradeType,
                x.Quantity,
                x.UnitPrice,
                x.TotalAmount,
                x.ExecutedAt,
                x.Stock.TrackedPlayer.Username,
                x.Stock.TrackedPlayer.AvatarUrl))
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetSharesTradedSinceAsync(
        Guid stockId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        // Uses ix_trade_stock_executed (stock_id, executed_at).
        return await dbContext.Trades
            .AsNoTracking()
            .Where(x => x.StockId == stockId && x.ExecutedAt >= since)
            .Select(x => (decimal?)x.Quantity)
            .SumAsync(cancellationToken) ?? 0m;
    }
}


