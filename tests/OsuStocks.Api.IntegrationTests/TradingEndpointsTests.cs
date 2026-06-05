using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class TradingEndpointsTests(PostgresTestcontainerFixture fixture)
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task BuyAndSellFlow_UpdatesWallet_TracksImmutableLedger_AndRecordsPriceHistory()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid stockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(TestUserId, 502001));

            dbContext.Wallets.Add(new Wallet
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
            dbContext.Portfolios.Add(portfolio);

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
            dbContext.TrackedPlayers.Add(trackedPlayer);

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
            dbContext.PlayerStocks.Add(stock);
            stockId = stock.Id;

            await dbContext.SaveChangesAsync();
        }

        var buyResponse = await client.PostAsJsonAsync(
            "/api/v1/trading/buy",
            new TradeRequest(stockId, 500));

        buyResponse.EnsureSuccessStatusCode();

        decimal walletAfterBuyBalance;
        Guid walletId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var walletAfterBuy = await dbContext.Wallets.AsNoTracking().SingleAsync(x => x.UserId == TestUserId);

            walletAfterBuyBalance = walletAfterBuy.Balance;
            walletId = walletAfterBuy.Id;

            var walletTransactionsAfterBuy = await dbContext.WalletTransactions
                .AsNoTracking()
                .Where(x => x.WalletId == walletAfterBuy.Id)
                .ToListAsync();

            var buyLedgerEntry = Assert.Single(walletTransactionsAfterBuy);
            Assert.Equal(WalletTransactionType.BuyStock, buyLedgerEntry.TransactionType);
            Assert.Equal(1000m, buyLedgerEntry.Amount);
        }

        Assert.Equal(999000m, walletAfterBuyBalance);

        var sellResponse = await client.PostAsJsonAsync(
            "/api/v1/trading/sell",
            new TradeRequest(stockId, 500));

        sellResponse.EnsureSuccessStatusCode();

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

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var walletAfterSell = await dbContext.Wallets.AsNoTracking().SingleAsync(x => x.UserId == TestUserId);

            var trades = await dbContext.Trades
                .AsNoTracking()
                .Where(x => x.UserId == TestUserId)
                .ToListAsync();

            Assert.Equal(2, trades.Count);

            var buyTrade = trades.Single(x => x.TradeType == TradeType.Buy);
            var sellTrade = trades.Single(x => x.TradeType == TradeType.Sell);

            var expectedBalance = 1_000_000m - buyTrade.TotalAmount + sellTrade.TotalAmount;
            Assert.Equal(expectedBalance, walletAfterSell.Balance);

            var ledgerAfterSell = await dbContext.WalletTransactions
                .AsNoTracking()
                .Where(x => x.WalletId == walletId)
                .ToListAsync();

            Assert.Equal(2, ledgerAfterSell.Count);
            Assert.Contains(ledgerAfterSell, x => x.TransactionType == WalletTransactionType.BuyStock);
            Assert.Contains(ledgerAfterSell, x => x.TransactionType == WalletTransactionType.SellStock);

            var updatedStock = await dbContext.PlayerStocks.AsNoTracking().SingleAsync(x => x.Id == stockId);
            Assert.True(updatedStock.CurrentPrice >= 1m);

            var priceHistory = await dbContext.StockPriceHistory
                .AsNoTracking()
                .Where(x => x.StockId == stockId)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

            Assert.Equal(2, priceHistory.Count);
            Assert.Equal(PriceChangeReason.BuyPressure, priceHistory[0].Reason);
            Assert.Equal(PriceChangeReason.SellPressure, priceHistory[1].Reason);
        }
    }

    [Fact]
    public async Task BuyStock_WithInsufficientBalance_ReturnsBadRequest()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid stockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(TestUserId, 502002));

            dbContext.Wallets.Add(new Wallet
            {
                Id = Guid.NewGuid(),
                UserId = TestUserId,
                Balance = 50m,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            });

            dbContext.Portfolios.Add(new Portfolio
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
            dbContext.TrackedPlayers.Add(trackedPlayer);

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

            dbContext.PlayerStocks.Add(stock);
            stockId = stock.Id;

            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading/buy",
            new TradeRequest(stockId, 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("INSUFFICIENT_BALANCE", error.Code);
    }

    private static User CreateUser(Guid userId, long osuUserId)
    {
        return new User
        {
            Id = userId,
            OsuUserId = osuUserId,
            Username = $"user-{osuUserId}",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        };
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
