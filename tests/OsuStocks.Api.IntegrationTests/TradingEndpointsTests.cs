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
        decimal rewardCreditsAfterBuy;
        decimal buyFee;
        decimal buyTradeAmount;
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

            // The first buy also unlocks the first-trade achievement, which credits a reward via its
            // own ledger entry. Assert the trade entry specifically and account for reward credits.
            var buyLedgerEntry = Assert.Single(
                walletTransactionsAfterBuy, x => x.TransactionType == WalletTransactionType.BuyStock);
            buyTradeAmount = buyLedgerEntry.Amount;
            // 500 sh @ price 2 = 1000 naive; slippage (price impact) + the liquidity bid/ask spread make
            // the buyer pay more than that. Exact value depends on the tuned coefficients, so assert the
            // surcharge rather than a brittle constant.
            Assert.True(buyTradeAmount > 1000m);

            // Progressive service fee, burned via its own TradeFee ledger entry (charged on top).
            buyFee = walletTransactionsAfterBuy
                .Where(x => x.TransactionType == WalletTransactionType.TradeFee)
                .Sum(x => x.Amount);
            Assert.True(buyFee > 0m);

            rewardCreditsAfterBuy = walletTransactionsAfterBuy
                .Where(x => x.TransactionType is WalletTransactionType.AchievementReward
                    or WalletTransactionType.MissionReward)
                .Sum(x => x.Amount);
        }

        Assert.Equal(1_000_000m - buyTradeAmount - buyFee + rewardCreditsAfterBuy, walletAfterBuyBalance);

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

            // Anti-exploit: average-fill slippage makes an immediate buy->sell round trip a net loss,
            // so a user can never profit from the price pump their own buy creates (pump-and-dump).
            Assert.True(buyTrade.TotalAmount > sellTrade.TotalAmount);

            var ledgerAfterSell = await dbContext.WalletTransactions
                .AsNoTracking()
                .Where(x => x.WalletId == walletId)
                .ToListAsync();

            // Reward credits (e.g. the first-trade achievement) post their own ledger entries and
            // raise the balance, so factor them in and assert the trade ledger entries specifically.
            var rewardCredits = ledgerAfterSell
                .Where(x => x.TransactionType is WalletTransactionType.AchievementReward
                    or WalletTransactionType.MissionReward)
                .Sum(x => x.Amount);

            // Trade fees (buy + sell) are burned via TradeFee ledger entries, so subtract them.
            var totalFees = ledgerAfterSell
                .Where(x => x.TransactionType == WalletTransactionType.TradeFee)
                .Sum(x => x.Amount);

            var expectedBalance = 1_000_000m - buyTrade.TotalAmount + sellTrade.TotalAmount - totalFees + rewardCredits;
            Assert.Equal(expectedBalance, walletAfterSell.Balance);

            var tradeLedger = ledgerAfterSell
                .Where(x => x.TransactionType is WalletTransactionType.BuyStock
                    or WalletTransactionType.SellStock)
                .ToList();
            Assert.Equal(2, tradeLedger.Count);
            Assert.Contains(tradeLedger, x => x.TransactionType == WalletTransactionType.BuyStock);
            Assert.Contains(tradeLedger, x => x.TransactionType == WalletTransactionType.SellStock);

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

    [Fact]
    public async Task ConcurrentBuys_ForSameUser_AllowExactlyOnePurchase_NoDoubleSpend()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid stockId;

        // Balance affords exactly one buy of 100 shares: naive cost is 100 * price(2) = 200, and
        // per-order price impact is capped (~10%) plus the bid/ask spread and the service fee — well
        // under 300. A second buy (another ~200+) is impossible, so at most one trade can ever clear.
        const decimal startingBalance = 300m;
        const int quantity = 100;
        const decimal stockPrice = 2m;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(TestUserId, 502003));

            dbContext.Wallets.Add(new Wallet
            {
                Id = Guid.NewGuid(),
                UserId = TestUserId,
                Balance = startingBalance,
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
                OsuUserId = 779,
                Username = "player-779",
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
                CurrentPrice = stockPrice,
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

        // Fire two identical buys concurrently. Both read the same wallet row version; the wallet is an
        // optimistic-concurrency entity, so only one commit can win — the loser either fails the
        // concurrency check (409 Conflict) or, if it re-reads after the winner commits, runs out of
        // balance (INSUFFICIENT_BALANCE). Either way exactly one purchase clears: no double-spend.
        var firstBuyTask = client.PostAsJsonAsync(
            "/api/v1/trading/buy", new TradeRequest(stockId, quantity));
        var secondBuyTask = client.PostAsJsonAsync(
            "/api/v1/trading/buy", new TradeRequest(stockId, quantity));

        var responses = await Task.WhenAll(firstBuyTask, secondBuyTask);

        var successes = responses.Where(r => r.IsSuccessStatusCode).ToList();
        var failures = responses.Where(r => !r.IsSuccessStatusCode).ToList();

        Assert.Single(successes);
        var failure = Assert.Single(failures);

        if (failure.StatusCode == HttpStatusCode.BadRequest)
        {
            // The loser re-read the wallet post-commit and could no longer afford the buy.
            var error = await failure.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(error);
            Assert.Equal("INSUFFICIENT_BALANCE", error.Code);
        }
        else
        {
            // The loser lost the optimistic-concurrency race on the shared wallet row.
            Assert.Equal(HttpStatusCode.Conflict, failure.StatusCode);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Exactly one buy trade was recorded.
            var trades = await dbContext.Trades
                .AsNoTracking()
                .Where(x => x.UserId == TestUserId && x.TradeType == TradeType.Buy)
                .ToListAsync();
            var trade = Assert.Single(trades);

            var wallet = await dbContext.Wallets.AsNoTracking().SingleAsync(x => x.UserId == TestUserId);

            var ledger = await dbContext.WalletTransactions
                .AsNoTracking()
                .Where(x => x.WalletId == wallet.Id)
                .ToListAsync();

            // Exactly one BuyStock ledger entry — no money created or lost by the rejected request.
            var buyLedger = Assert.Single(
                ledger, x => x.TransactionType == WalletTransactionType.BuyStock);
            Assert.Equal(trade.TotalAmount, buyLedger.Amount);

            var fees = ledger
                .Where(x => x.TransactionType == WalletTransactionType.TradeFee)
                .Sum(x => x.Amount);
            var rewardCredits = ledger
                .Where(x => x.TransactionType is WalletTransactionType.AchievementReward
                    or WalletTransactionType.MissionReward)
                .Sum(x => x.Amount);

            // Final balance reflects exactly one purchase: starting - one trade - its fees + rewards.
            Assert.Equal(startingBalance - trade.TotalAmount - fees + rewardCredits, wallet.Balance);
        }
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
        [property: JsonPropertyName("quantity")] decimal Quantity,
        [property: JsonPropertyName("averagePrice")] decimal AveragePrice);

    private sealed record HistoryEnvelope([property: JsonPropertyName("items")] List<HistoryItem> Items);

    private sealed record HistoryItem([property: JsonPropertyName("tradeType")] string TradeType);

    private sealed record ErrorResponse(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message);
}
