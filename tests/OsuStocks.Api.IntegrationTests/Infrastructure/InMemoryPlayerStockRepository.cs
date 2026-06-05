using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryPlayerStockRepository : IPlayerStockRepository
{
    private readonly ConcurrentDictionary<Guid, PlayerStock> _stocksById = new();
    private readonly ConcurrentDictionary<Guid, Guid> _stockIdsByTrackedPlayerId = new();

    public Task<PlayerStock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _stocksById.TryGetValue(id, out var stock);
        return Task.FromResult(Clone(stock));
    }

    public Task<PlayerStock?> GetByTrackedPlayerIdAsync(Guid trackedPlayerId, CancellationToken cancellationToken = default)
    {
        if (!_stockIdsByTrackedPlayerId.TryGetValue(trackedPlayerId, out var stockId))
        {
            return Task.FromResult<PlayerStock?>(null);
        }

        _stocksById.TryGetValue(stockId, out var stock);
        return Task.FromResult(Clone(stock));
    }

    public Task<IReadOnlyList<PlayerStock>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var items = _stocksById.Values
            .OrderByDescending(x => x.CurrentPrice)
            .Skip(Math.Max(page - 1, 0) * pageSize)
            .Take(Math.Max(pageSize, 0))
            .Select(Clone)
            .Cast<PlayerStock>()
            .ToList();

        return Task.FromResult<IReadOnlyList<PlayerStock>>(items);
    }

    public Task AddAsync(PlayerStock playerStock, CancellationToken cancellationToken = default)
    {
        _stocksById[playerStock.Id] = Clone(playerStock)!;
        _stockIdsByTrackedPlayerId[playerStock.TrackedPlayerId] = playerStock.Id;
        return Task.CompletedTask;
    }

    public void Update(PlayerStock playerStock)
    {
        _stocksById[playerStock.Id] = Clone(playerStock)!;
        _stockIdsByTrackedPlayerId[playerStock.TrackedPlayerId] = playerStock.Id;
    }

    private static PlayerStock? Clone(PlayerStock? stock)
    {
        if (stock is null)
        {
            return null;
        }

        return new PlayerStock
        {
            Id = stock.Id,
            TrackedPlayerId = stock.TrackedPlayerId,
            CurrentPrice = stock.CurrentPrice,
            DemandScore = stock.DemandScore,
            PerformanceScore = stock.PerformanceScore,
            CreatedAt = stock.CreatedAt,
            CreatedBy = stock.CreatedBy,
            LastUpdated = stock.LastUpdated,
            UpdatedAt = stock.UpdatedAt,
            UpdatedBy = stock.UpdatedBy
        };
    }
}
