using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class TradingEndpointsTests
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task BuyAndSellFlow_UpdatesWallet_TracksImmutableLedger_AndRecordsPriceHistory()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var walletRepository = scope.ServiceProvider.GetRequiredService<InMemoryWalletRepository>();
        var portfolioRepository = scope.ServiceProvider.GetRequiredService<InMemoryPortfolioRepository>();
        var trackedPlayerRepository = scope.ServiceProvider.GetRequiredService<InMemoryTrackedPlayerRepository>();
        var playerStockRepository = scope.ServiceProvider.GetRequiredService<InMemoryPlayerStockRepository>();
        var walletTransactionRepository = scope.ServiceProvider.GetRequiredService<InMemoryWalletTransactionRepository>();
        var stockPriceHistoryRepository = scope.ServiceProvider.GetRequiredService<InMemoryStockPriceHistoryRepository>();

        await walletRepository.AddAsync(new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            Balance = 1_000_000m,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        });

        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        };
        await portfolioRepository.AddAsync(portfolio);

        var trackedPlayer = new TrackedPlayer
        {
            Id = Guid.NewGuid(),
            OsuUserId = 777,
            Username = "player-777",
            TrackingTier = TrackingTier.Tier2,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        };
        await trackedPlayerRepository.AddAsync(trackedPlayer);

        var stock = new PlayerStock
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = trackedPlayer.Id,
            CurrentPrice = 2m,
            DemandScore = 1m,
            PerformanceScore = 1m,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        };
        await playerStockRepository.AddAsync(stock);

        var buyResponse = await client.PostAsJsonAsync(
            "/api/v1/trading/buy",
            new TradeRequest(stock.Id, 500));

        buyResponse.EnsureSuccessStatusCode();

        var walletAfterBuy = await walletRepository.GetByUserIdAsync(TestUserId);
        Assert.NotNull(walletAfterBuy);
        Assert.Equal(999000m, walletAfterBuy.Balance);

        var walletTransactionsAfterBuy = await walletTransactionRepository.GetByWalletIdAsync(walletAfterBuy.Id, 0, 10);
        var buyLedgerEntry = Assert.Single(walletTransactionsAfterBuy);
        Assert.Equal(WalletTransactionType.BuyStock, buyLedgerEntry.TransactionType);
        Assert.Equal(1000m, buyLedgerEntry.Amount);

        var sellResponse = await client.PostAsJsonAsync(
            "/api/v1/trading/sell",
            new TradeRequest(stock.Id, 500));

        sellResponse.EnsureSuccessStatusCode();

        var walletAfterSell = await walletRepository.GetByUserIdAsync(TestUserId);
        Assert.NotNull(walletAfterSell);
        Assert.Equal(1_001_250m, walletAfterSell.Balance);

        var historyResponse = await client.GetAsync("/api/v1/trading/history?page=1&pageSize=10");
        historyResponse.EnsureSuccessStatusCode();

        var history = await historyResponse.Content.ReadFromJsonAsync<HistoryEnvelope>();
        Assert.NotNull(history);
        Assert.Equal(2, history.Items.Count);
        Assert.Equal("Sell", history.Items[0].TradeType);
        Assert.Equal("Buy", history.Items[1].TradeType);

        var holdingsAfterSellResponse = await client.GetAsync("/api/v1/portfolio/holdings");
        holdingsAfterSellResponse.EnsureSuccessStatusCode();

        var holdingsAfterSell = await holdingsAfterSellResponse.Content.ReadFromJsonAsync<HoldingsEnvelope>();
        Assert.NotNull(holdingsAfterSell);
        Assert.Empty(holdingsAfterSell.Items);

        var ledgerAfterSell = await walletTransactionRepository.GetByWalletIdAsync(walletAfterSell.Id, 0, 10);
        Assert.Equal(2, ledgerAfterSell.Count);
        Assert.Contains(ledgerAfterSell, x => x.TransactionType == WalletTransactionType.BuyStock && x.Id == buyLedgerEntry.Id && x.Amount == buyLedgerEntry.Amount);
        Assert.Contains(ledgerAfterSell, x => x.TransactionType == WalletTransactionType.SellStock);

        var updatedStock = await playerStockRepository.GetByIdAsync(stock.Id);
        Assert.NotNull(updatedStock);
        Assert.True(updatedStock.CurrentPrice >= 1m);

        var priceHistory = stockPriceHistoryRepository.GetAllForStock(stock.Id);
        Assert.Equal(2, priceHistory.Count);
        Assert.Equal(PriceChangeReason.BuyPressure, priceHistory[0].Reason);
        Assert.Equal(PriceChangeReason.SellPressure, priceHistory[1].Reason);
    }

    [Fact]
    public async Task BuyStock_WithInsufficientBalance_ReturnsBadRequest()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var walletRepository = scope.ServiceProvider.GetRequiredService<InMemoryWalletRepository>();
        var portfolioRepository = scope.ServiceProvider.GetRequiredService<InMemoryPortfolioRepository>();
        var trackedPlayerRepository = scope.ServiceProvider.GetRequiredService<InMemoryTrackedPlayerRepository>();
        var playerStockRepository = scope.ServiceProvider.GetRequiredService<InMemoryPlayerStockRepository>();

        await walletRepository.AddAsync(new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            Balance = 50m,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        });

        await portfolioRepository.AddAsync(new Portfolio
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        });

        var trackedPlayer = new TrackedPlayer
        {
            Id = Guid.NewGuid(),
            OsuUserId = 778,
            Username = "player-778",
            TrackingTier = TrackingTier.Tier2,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        };
        await trackedPlayerRepository.AddAsync(trackedPlayer);

        var stock = new PlayerStock
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = trackedPlayer.Id,
            CurrentPrice = 100m,
            DemandScore = 1m,
            PerformanceScore = 1m,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        };
        await playerStockRepository.AddAsync(stock);

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading/buy",
            new TradeRequest(stock.Id, 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("INSUFFICIENT_BALANCE", error.Code);
    }

    private sealed record TradeRequest(Guid StockId, int Quantity);

    private sealed record HoldingsEnvelope([property: JsonPropertyName("items")] List<HoldingItem> Items);
    private sealed record HoldingItem(
        [property: JsonPropertyName("quantity")] int Quantity,
        [property: JsonPropertyName("averagePrice")] decimal AveragePrice);

    private sealed record HistoryEnvelope([property: JsonPropertyName("items")] List<HistoryItem> Items);
    private sealed record HistoryItem([property: JsonPropertyName("tradeType")] string TradeType);

    private sealed record ErrorResponse(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message);
}

