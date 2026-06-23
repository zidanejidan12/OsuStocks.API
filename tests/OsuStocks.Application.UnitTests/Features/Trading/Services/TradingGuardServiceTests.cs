using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.Trading.Services;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.Trading.Services;

public sealed class TradingGuardServiceTests
{
    private readonly InMemoryTradeRepo _tradeRepo = new();
    private readonly InMemoryHoldingRepo _holdingRepo = new();

    // --- Cooldown Tests ---

    [Fact]
    public async Task CheckCooldown_NoExistingTrade_ReturnsSuccess()
    {
        var service = CreateService(cooldownSeconds: 30);

        var result = await service.CheckCooldownAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckCooldown_TradeWithinCooldown_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var stockId = Guid.NewGuid();

        await _tradeRepo.AddAsync(new Trade
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StockId = stockId,
            TradeType = TradeType.Buy,
            Quantity = 1,
            UnitPrice = 100,
            TotalAmount = 100,
            ExecutedAt = DateTimeOffset.UtcNow.AddSeconds(-10)
        });

        var service = CreateService(cooldownSeconds: 30);

        var result = await service.CheckCooldownAsync(userId, stockId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TRADE_COOLDOWN", result.Error!.Code);
    }

    [Fact]
    public async Task CheckCooldown_TradeAfterCooldown_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        var stockId = Guid.NewGuid();

        await _tradeRepo.AddAsync(new Trade
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StockId = stockId,
            TradeType = TradeType.Buy,
            Quantity = 1,
            UnitPrice = 100,
            TotalAmount = 100,
            ExecutedAt = DateTimeOffset.UtcNow.AddSeconds(-60)
        });

        var service = CreateService(cooldownSeconds: 30);

        var result = await service.CheckCooldownAsync(userId, stockId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckCooldown_DifferentStock_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();

        await _tradeRepo.AddAsync(new Trade
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StockId = Guid.NewGuid(),
            TradeType = TradeType.Buy,
            Quantity = 1,
            UnitPrice = 100,
            TotalAmount = 100,
            ExecutedAt = DateTimeOffset.UtcNow.AddSeconds(-5)
        });

        var service = CreateService(cooldownSeconds: 30);

        var result = await service.CheckCooldownAsync(userId, Guid.NewGuid());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckCooldown_DisabledWithZero_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        var stockId = Guid.NewGuid();

        await _tradeRepo.AddAsync(new Trade
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StockId = stockId,
            TradeType = TradeType.Buy,
            Quantity = 1,
            UnitPrice = 100,
            TotalAmount = 100,
            ExecutedAt = DateTimeOffset.UtcNow
        });

        var service = CreateService(cooldownSeconds: 0);

        var result = await service.CheckCooldownAsync(userId, stockId);

        Assert.True(result.IsSuccess);
    }

    // --- Position Limit Tests ---

    [Fact]
    public async Task CheckPositionLimit_WithinLimit_ReturnsSuccess()
    {
        var stockId = Guid.NewGuid();

        // Other users hold 80 shares
        await _holdingRepo.AddAsync(new Holding
        {
            Id = Guid.NewGuid(),
            PortfolioId = Guid.NewGuid(),
            StockId = stockId,
            Quantity = 80,
            AveragePrice = 100,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // referenceSupplyShares: 0 isolates the pure percentage math (no virtual supply).
        var service = CreateService(maxOwnershipPercentage: 25, referenceSupplyShares: 0);

        // Buying 20 shares with 0 existing → 20/(80+20) = 20%
        var result = await service.CheckPositionLimitAsync(
            Guid.NewGuid(), stockId, 20, 0);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckPositionLimit_ExceedsLimit_ReturnsFailure()
    {
        var stockId = Guid.NewGuid();

        // Other users hold 60 shares
        await _holdingRepo.AddAsync(new Holding
        {
            Id = Guid.NewGuid(),
            PortfolioId = Guid.NewGuid(),
            StockId = stockId,
            Quantity = 60,
            AveragePrice = 100,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // referenceSupplyShares: 0 isolates the pure percentage math (no virtual supply).
        var service = CreateService(maxOwnershipPercentage: 25, referenceSupplyShares: 0);

        // User already holds 10, buying 20 more → 30/(60+20) = 37.5%
        var result = await service.CheckPositionLimitAsync(
            Guid.NewGuid(), stockId, 20, 10);

        Assert.False(result.IsSuccess);
        Assert.Equal("POSITION_LIMIT_EXCEEDED", result.Error!.Code);
        // Message tells the user the exact max they can still buy:
        // largest q with (10+q)/(60+q) <= 0.25  ->  (0.25*60 - 10)/0.75 = 6.66 (floored to 2dp).
        Assert.Contains("6.66", result.Error.Message);
    }

    [Fact]
    public async Task CheckPositionLimit_FirstBuyerWithReferenceSupply_ReturnsSuccess()
    {
        // With a virtual reference supply, a first buyer on a brand-new stock (real float 0)
        // is measured against (0 + reference). Buying 10 with reference 100 → 10/(100+10) = 9.1% ≤ 25%.
        // This replaces the old "first buyer bypasses the cap entirely" rule.
        var service = CreateService(maxOwnershipPercentage: 25, referenceSupplyShares: 100);

        var result = await service.CheckPositionLimitAsync(
            Guid.NewGuid(), Guid.NewGuid(), 10, 0);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckPositionLimit_FirstBuyerHugeQuantity_ExceedsLimit()
    {
        // The reference supply also caps the FIRST buyer (closing the old "first buyer can own 100%"
        // hole): buying 1000 on an empty stock with reference 100 → 1000/(100+1000) = 90.9% > 25%.
        var service = CreateService(maxOwnershipPercentage: 25, referenceSupplyShares: 100);

        var result = await service.CheckPositionLimitAsync(
            Guid.NewGuid(), Guid.NewGuid(), 1000, 0);

        Assert.False(result.IsSuccess);
        Assert.Equal("POSITION_LIMIT_EXCEEDED", result.Error!.Code);
    }

    [Fact]
    public async Task CheckPositionLimit_ThinStock_AllowsAdditionalBuyers()
    {
        var stockId = Guid.NewGuid();

        // A "gatekept" stock: someone bought only 2 shares, so the real float is 2.
        await _holdingRepo.AddAsync(new Holding
        {
            Id = Guid.NewGuid(),
            PortfolioId = Guid.NewGuid(),
            StockId = stockId,
            Quantity = 2,
            AveragePrice = 100,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var service = CreateService(maxOwnershipPercentage: 25, referenceSupplyShares: 100);

        // A new buyer takes 20 shares → 20/(2+100+20) = 16.4% ≤ 25%, so it's allowed.
        // (Under the old float-only formula this was 20/(2+20) = 90.9% and would be blocked.)
        var result = await service.CheckPositionLimitAsync(
            Guid.NewGuid(), stockId, 20, 0);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckPositionLimit_DisabledWithHundredPercent_ReturnsSuccess()
    {
        var service = CreateService(maxOwnershipPercentage: 100);

        var result = await service.CheckPositionLimitAsync(
            Guid.NewGuid(), Guid.NewGuid(), 1000, 0);

        Assert.True(result.IsSuccess);
    }

    // --- Rapid Trading Tests (logging only, no blocking) ---

    [Fact]
    public async Task CheckRapidTrading_BelowThreshold_NoException()
    {
        var service = CreateService(rapidTradeThreshold: 10, rapidTradeWindowSeconds: 300);

        // Should not throw
        await service.CheckRapidTradingAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task CheckRapidTrading_AboveThreshold_LogsButDoesNotThrow()
    {
        var userId = Guid.NewGuid();

        for (var i = 0; i < 15; i++)
        {
            await _tradeRepo.AddAsync(new Trade
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StockId = Guid.NewGuid(),
                TradeType = TradeType.Buy,
                Quantity = 1,
                UnitPrice = 100,
                TotalAmount = 100,
                ExecutedAt = DateTimeOffset.UtcNow.AddSeconds(-i)
            });
        }

        var service = CreateService(rapidTradeThreshold: 10, rapidTradeWindowSeconds: 300);

        // Should not throw, just log
        await service.CheckRapidTradingAsync(userId);
    }

    private TradingGuardService CreateService(
        decimal maxOwnershipPercentage = 25,
        decimal referenceSupplyShares = 100,
        int cooldownSeconds = 30,
        int rapidTradeWindowSeconds = 300,
        int rapidTradeThreshold = 10)
    {
        return new TradingGuardService(
            _tradeRepo,
            _holdingRepo,
            new StubAntiAbuseSettings(maxOwnershipPercentage, referenceSupplyShares, cooldownSeconds, rapidTradeWindowSeconds, rapidTradeThreshold),
            NullLogger<TradingGuardService>.Instance);
    }

    private sealed class StubAntiAbuseSettings(
        decimal maxOwnershipPercentage,
        decimal referenceSupplyShares,
        int tradeCooldownSeconds,
        int rapidTradeWindowSeconds,
        int rapidTradeThreshold) : IAntiAbuseSettings
    {
        public decimal MaxOwnershipPercentage => maxOwnershipPercentage;
        public decimal ReferenceSupplyShares => referenceSupplyShares;
        public int TradeCooldownSeconds => tradeCooldownSeconds;
        public int RapidTradeWindowSeconds => rapidTradeWindowSeconds;
        public int RapidTradeThreshold => rapidTradeThreshold;
    }

    private sealed class InMemoryTradeRepo : ITradeRepository
    {
        private readonly ConcurrentBag<Trade> _trades = [];

        public Task AddAsync(Trade trade, CancellationToken cancellationToken = default)
        {
            _trades.Add(trade);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Trade>> GetByUserIdAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Trade>>([]);

        public Task<Trade?> GetLastByUserAndStockAsync(Guid userId, Guid stockId, CancellationToken cancellationToken = default)
        {
            var trade = _trades
                .Where(x => x.UserId == userId && x.StockId == stockId)
                .OrderByDescending(x => x.ExecutedAt)
                .FirstOrDefault();

            return Task.FromResult(trade);
        }

        public Task<int> CountRecentByUserAsync(Guid userId, DateTimeOffset since, CancellationToken cancellationToken = default)
        {
            var count = _trades.Count(x => x.UserId == userId && x.ExecutedAt >= since);
            return Task.FromResult(count);
        }

        public Task<bool> ExistsByStockAsync(Guid stockId, CancellationToken cancellationToken = default)
            => Task.FromResult(_trades.Any(x => x.StockId == stockId));
    }

    private sealed class InMemoryHoldingRepo : IHoldingRepository
    {
        private readonly ConcurrentBag<Holding> _holdings = [];

        public Task<Holding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Holding?>(null);

        public Task<Holding?> GetByPortfolioAndStockAsync(Guid portfolioId, Guid stockId, CancellationToken cancellationToken = default)
            => Task.FromResult<Holding?>(null);

        public Task<IReadOnlyList<Holding>> GetByPortfolioIdAsync(Guid portfolioId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Holding>>([]);

        public Task<decimal> GetTotalQuantityByStockAsync(Guid stockId, CancellationToken cancellationToken = default)
        {
            var total = _holdings.Where(x => x.StockId == stockId).Sum(x => x.Quantity);
            return Task.FromResult(total);
        }

        public Task AddAsync(Holding holding, CancellationToken cancellationToken = default)
        {
            _holdings.Add(holding);
            return Task.CompletedTask;
        }

        public void Update(Holding holding) { }
        public void Remove(Holding holding) { }
    }
}
