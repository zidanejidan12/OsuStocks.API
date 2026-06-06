using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryTradeRepository : ITradeRepository
{
    private readonly ConcurrentDictionary<Guid, Trade> _tradesById = new();

    public Task AddAsync(Trade trade, CancellationToken cancellationToken = default)
    {
        _tradesById[trade.Id] = Clone(trade)!;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Trade>> GetByUserIdAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var items = _tradesById.Values
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.ExecutedAt)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Max(take, 0))
            .Select(Clone)
            .Cast<Trade>()
            .ToList();

        return Task.FromResult<IReadOnlyList<Trade>>(items);
    }

    public Task<Trade?> GetLastByUserAndStockAsync(Guid userId, Guid stockId, CancellationToken cancellationToken = default)
    {
        var trade = _tradesById.Values
            .Where(x => x.UserId == userId && x.StockId == stockId)
            .OrderByDescending(x => x.ExecutedAt)
            .FirstOrDefault();

        return Task.FromResult(Clone(trade));
    }

    public Task<int> CountRecentByUserAsync(Guid userId, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var count = _tradesById.Values
            .Count(x => x.UserId == userId && x.ExecutedAt >= since);

        return Task.FromResult(count);
    }

    private static Trade? Clone(Trade? trade)
    {
        if (trade is null)
        {
            return null;
        }

        return new Trade
        {
            Id = trade.Id,
            UserId = trade.UserId,
            StockId = trade.StockId,
            TradeType = trade.TradeType,
            Quantity = trade.Quantity,
            UnitPrice = trade.UnitPrice,
            TotalAmount = trade.TotalAmount,
            ExecutedAt = trade.ExecutedAt
        };
    }
}
