using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.Market.Services;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Market.Interfaces;
using OsuStocks.Domain.Market.Models;
using OsuStocks.Domain.Market.Services;
using OsuStocks.Domain.Repositories;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.Market.Services;

public sealed class MarketEventProcessingServiceTests
{
    [Fact]
    public async Task ApplyForStockAsync_BuyOrder_UpdatesStockAndRecordsHistory()
    {
        var stock = new PlayerStock
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = Guid.NewGuid(),
            CurrentPrice = 100m,
            LastUpdated = DateTimeOffset.UtcNow
        };

        var stockRepository = new InMemoryPlayerStockRepository(stock);
        var historyRepository = new InMemoryStockPriceHistoryRepository();
        var dbContext = new CountingDbContext();
        var coefficientsProvider = new StaticMarketCoefficientsProvider(new MarketPricingCoefficients(
            0.01m,
            0.01m,
            0.6m,
            0.10m,
            0.005m,
            0.001m,
            0.10m,
            0.005m,
            1m,
            0.5m,
            0.05m,
            0.10m,
            1000m,
            0.02m,
            0.001m));

        IMarketEventProcessingService service = new MarketEventProcessingService(
            stockRepository,
            historyRepository,
            dbContext,
            coefficientsProvider,
            new MarketPriceEngine());

        var changed = await service.ApplyForStockAsync(
            stock.Id,
            MarketPriceInput.Buy(3),
            DateTimeOffset.UtcNow);

        Assert.NotNull(changed);
        Assert.Equal(103m, changed.NewPrice);

        var updated = await stockRepository.GetByIdAsync(stock.Id);
        Assert.NotNull(updated);
        Assert.Equal(103m, updated.CurrentPrice);

        var history = Assert.Single(historyRepository.Items);
        Assert.Equal(stock.Id, history.StockId);
        Assert.Equal(PriceChangeReason.BuyPressure, history.Reason);
        Assert.Equal(100m, history.PreviousPrice);
        Assert.Equal(103m, history.NewPrice);
        Assert.Equal(1, dbContext.SaveChangesCalls);
    }

    [Fact]
    public async Task ApplyForTrackedPlayerAsync_Inactivity_UsesDecayReasonAndFloor()
    {
        var trackedPlayerId = Guid.NewGuid();
        var stock = new PlayerStock
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = trackedPlayerId,
            CurrentPrice = 1.2m,
            LastUpdated = DateTimeOffset.UtcNow
        };

        var stockRepository = new InMemoryPlayerStockRepository(stock);
        var historyRepository = new InMemoryStockPriceHistoryRepository();
        var dbContext = new CountingDbContext();
        var coefficientsProvider = new StaticMarketCoefficientsProvider(new MarketPricingCoefficients(
            0.01m,
            0.01m,
            0.6m,
            0.10m,
            0.005m,
            0.001m,
            0.10m,
            0.50m,
            1m,
            0.5m,
            0.05m,
            0.10m,
            1000m,
            0.02m,
            0.001m));

        IMarketEventProcessingService service = new MarketEventProcessingService(
            stockRepository,
            historyRepository,
            dbContext,
            coefficientsProvider,
            new MarketPriceEngine());

        var changed = await service.ApplyForTrackedPlayerAsync(
            trackedPlayerId,
            MarketPriceInput.Inactivity(),
            DateTimeOffset.UtcNow);

        Assert.NotNull(changed);
        Assert.Equal(PriceChangeReason.Decay, changed.Reason);
        Assert.Equal(1m, changed.NewPrice);
    }

    private sealed class StaticMarketCoefficientsProvider(MarketPricingCoefficients coefficients) : IMarketCoefficientsProvider
    {
        public Task<MarketPricingCoefficients> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult(coefficients);
    }

    private sealed class CountingDbContext : IApplicationDbContext
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class InMemoryPlayerStockRepository(PlayerStock initialStock) : IPlayerStockRepository
    {
        private readonly Dictionary<Guid, PlayerStock> _stocks = new() { [initialStock.Id] = Clone(initialStock) };

        public Task<PlayerStock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _stocks.TryGetValue(id, out var stock);
            return Task.FromResult(stock is null ? null : Clone(stock));
        }

        public Task<PlayerStock?> GetByTrackedPlayerIdAsync(Guid trackedPlayerId, CancellationToken cancellationToken = default)
        {
            var stock = _stocks.Values.FirstOrDefault(x => x.TrackedPlayerId == trackedPlayerId);
            return Task.FromResult(stock is null ? null : Clone(stock));
        }

        public Task<IReadOnlyList<PlayerStock>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PlayerStock>>(_stocks.Values.Select(Clone).ToList());

        public Task AddAsync(PlayerStock playerStock, CancellationToken cancellationToken = default)
        {
            _stocks[playerStock.Id] = Clone(playerStock);
            return Task.CompletedTask;
        }

        public void Update(PlayerStock playerStock)
        {
            _stocks[playerStock.Id] = Clone(playerStock);
        }

        private static PlayerStock Clone(PlayerStock stock)
        {
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

    private sealed class InMemoryStockPriceHistoryRepository : IStockPriceHistoryRepository
    {
        private readonly List<StockPriceHistory> _items = [];

        public IReadOnlyList<StockPriceHistory> Items => _items;

        public Task AddAsync(StockPriceHistory historyEntry, CancellationToken cancellationToken = default)
        {
            _items.Add(new StockPriceHistory
            {
                Id = historyEntry.Id,
                StockId = historyEntry.StockId,
                PreviousPrice = historyEntry.PreviousPrice,
                NewPrice = historyEntry.NewPrice,
                Reason = historyEntry.Reason,
                CreatedAt = historyEntry.CreatedAt
            });

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StockPriceHistory>> GetLatestByStockIdAsync(
            Guid stockId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var items = _items
                .Where(x => x.StockId == stockId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToList();

            return Task.FromResult<IReadOnlyList<StockPriceHistory>>(items);
        }

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
        {
            var removed = _items.RemoveAll(x => x.CreatedAt < cutoff);
            return Task.FromResult(removed);
        }
    }
}

