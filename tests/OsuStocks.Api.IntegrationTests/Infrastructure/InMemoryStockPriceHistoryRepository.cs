using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryStockPriceHistoryRepository : IStockPriceHistoryRepository
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<StockPriceHistory>> _itemsByStock = new();

    public Task AddAsync(StockPriceHistory historyEntry, CancellationToken cancellationToken = default)
    {
        var queue = _itemsByStock.GetOrAdd(historyEntry.StockId, _ => new ConcurrentQueue<StockPriceHistory>());
        queue.Enqueue(Clone(historyEntry));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StockPriceHistory>> GetLatestByStockIdAsync(
        Guid stockId,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (!_itemsByStock.TryGetValue(stockId, out var queue))
        {
            return Task.FromResult<IReadOnlyList<StockPriceHistory>>([]);
        }

        var items = queue
            .Select(Clone)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<StockPriceHistory>>(items);
    }

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        var removed = 0;
        foreach (var stockId in _itemsByStock.Keys.ToList())
        {
            if (_itemsByStock.TryGetValue(stockId, out var queue))
            {
                var kept = queue.Where(x => x.CreatedAt >= cutoff).ToList();
                removed += queue.Count - kept.Count;
                _itemsByStock[stockId] = new ConcurrentQueue<StockPriceHistory>(kept);
            }
        }

        return Task.FromResult(removed);
    }

    public IReadOnlyList<StockPriceHistory> GetAllForStock(Guid stockId)
    {
        if (!_itemsByStock.TryGetValue(stockId, out var queue))
        {
            return [];
        }

        return queue.Select(Clone).OrderBy(x => x.CreatedAt).ToList();
    }

    private static StockPriceHistory Clone(StockPriceHistory entry)
    {
        return new StockPriceHistory
        {
            Id = entry.Id,
            StockId = entry.StockId,
            PreviousPrice = entry.PreviousPrice,
            NewPrice = entry.NewPrice,
            Reason = entry.Reason,
            CreatedAt = entry.CreatedAt
        };
    }
}
