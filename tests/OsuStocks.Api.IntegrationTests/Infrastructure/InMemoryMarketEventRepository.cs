using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryMarketEventRepository : IMarketEventRepository
{
    private readonly ConcurrentBag<MarketEvent> _events = [];

    public Task AddAsync(MarketEvent marketEvent, CancellationToken cancellationToken = default)
    {
        _events.Add(Clone(marketEvent));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MarketEvent>> GetLatestByStockIdAsync(
        Guid stockId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var items = _events
            .Where(x => x.StockId == stockId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(Clone)
            .ToList();

        return Task.FromResult<IReadOnlyList<MarketEvent>>(items);
    }

    public IReadOnlyList<MarketEvent> GetAll()
    {
        return _events.Select(Clone).OrderBy(x => x.CreatedAt).ToList();
    }

    private static MarketEvent Clone(MarketEvent entry)
    {
        return new MarketEvent
        {
            Id = entry.Id,
            StockId = entry.StockId,
            EventType = entry.EventType,
            Payload = entry.Payload,
            CreatedAt = entry.CreatedAt
        };
    }
}
