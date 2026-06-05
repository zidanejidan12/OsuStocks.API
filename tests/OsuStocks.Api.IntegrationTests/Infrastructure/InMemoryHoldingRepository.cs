using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryHoldingRepository : IHoldingRepository
{
    private readonly ConcurrentDictionary<Guid, Holding> _holdingsById = new();
    private readonly ConcurrentDictionary<string, Guid> _holdingIdsByPortfolioStock = new();

    public Task<Holding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _holdingsById.TryGetValue(id, out var holding);
        return Task.FromResult(Clone(holding));
    }

    public Task<Holding?> GetByPortfolioAndStockAsync(Guid portfolioId, Guid stockId, CancellationToken cancellationToken = default)
    {
        if (!_holdingIdsByPortfolioStock.TryGetValue(Key(portfolioId, stockId), out var holdingId))
        {
            return Task.FromResult<Holding?>(null);
        }

        _holdingsById.TryGetValue(holdingId, out var holding);
        return Task.FromResult(Clone(holding));
    }

    public Task<IReadOnlyList<Holding>> GetByPortfolioIdAsync(Guid portfolioId, CancellationToken cancellationToken = default)
    {
        var holdings = _holdingsById.Values
            .Where(x => x.PortfolioId == portfolioId)
            .OrderByDescending(x => x.Quantity)
            .Select(Clone)
            .Cast<Holding>()
            .ToList();

        return Task.FromResult<IReadOnlyList<Holding>>(holdings);
    }

    public Task AddAsync(Holding holding, CancellationToken cancellationToken = default)
    {
        _holdingsById[holding.Id] = Clone(holding)!;
        _holdingIdsByPortfolioStock[Key(holding.PortfolioId, holding.StockId)] = holding.Id;
        return Task.CompletedTask;
    }

    public void Update(Holding holding)
    {
        _holdingsById[holding.Id] = Clone(holding)!;
        _holdingIdsByPortfolioStock[Key(holding.PortfolioId, holding.StockId)] = holding.Id;
    }

    public void Remove(Holding holding)
    {
        _holdingsById.TryRemove(holding.Id, out _);
        _holdingIdsByPortfolioStock.TryRemove(Key(holding.PortfolioId, holding.StockId), out _);
    }

    private static string Key(Guid portfolioId, Guid stockId) => $"{portfolioId:N}:{stockId:N}";

    private static Holding? Clone(Holding? holding)
    {
        if (holding is null)
        {
            return null;
        }

        return new Holding
        {
            Id = holding.Id,
            PortfolioId = holding.PortfolioId,
            StockId = holding.StockId,
            Quantity = holding.Quantity,
            AveragePrice = holding.AveragePrice,
            RowVersion = holding.RowVersion,
            CreatedAt = holding.CreatedAt,
            CreatedBy = holding.CreatedBy,
            UpdatedAt = holding.UpdatedAt,
            UpdatedBy = holding.UpdatedBy
        };
    }
}
