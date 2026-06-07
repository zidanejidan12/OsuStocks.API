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
public sealed class MarketAnalyticsEndpointTests(PostgresTestcontainerFixture fixture)
{
    private const decimal CurrentPrice = 10m;

    // Three distinct trading users (distinct from the authed caller; analytics is global per-stock,
    // not per-user) so we can verify activeTraders24h counts distinct UserIds within 24h.
    private static readonly Guid TraderA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TraderB = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid TraderC = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

    [Fact]
    public async Task GetStockAnalytics_ReturnsWindowedVolumes_OwnershipMarketCap_AndPositiveVolatility()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid stockId;
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Three trading users.
            dbContext.Users.Add(CreateUser(TraderA, 900001));
            dbContext.Users.Add(CreateUser(TraderB, 900002));
            dbContext.Users.Add(CreateUser(TraderC, 900003));

            // Two distinct portfolios that will both hold the stock with quantity > 0.
            var portfolioOne = new Portfolio
            {
                Id = Guid.NewGuid(),
                UserId = TraderA,
                CreatedAt = now,
                CreatedBy = "seed"
            };
            var portfolioTwo = new Portfolio
            {
                Id = Guid.NewGuid(),
                UserId = TraderB,
                CreatedAt = now,
                CreatedBy = "seed"
            };
            // A third portfolio whose holding has quantity == 0 -> must NOT count towards ownership/marketcap.
            var portfolioEmpty = new Portfolio
            {
                Id = Guid.NewGuid(),
                UserId = TraderC,
                CreatedAt = now,
                CreatedBy = "seed"
            };
            dbContext.Portfolios.AddRange(portfolioOne, portfolioTwo, portfolioEmpty);

            var trackedPlayer = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = 900100,
                Username = "analytics-player",
                TrackingTier = TrackingTier.Tier1,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "seed"
            };
            dbContext.TrackedPlayers.Add(trackedPlayer);

            var stock = new PlayerStock
            {
                Id = Guid.NewGuid(),
                TrackedPlayerId = trackedPlayer.Id,
                CurrentPrice = CurrentPrice,
                DemandScore = 1m,
                PerformanceScore = 1m,
                CreatedAt = now,
                LastUpdated = now,
                CreatedBy = "seed"
            };
            dbContext.PlayerStocks.Add(stock);
            stockId = stock.Id;

            // --- Trades within the last 24h: two distinct users ---
            // TraderA: 10 shares @ 10 = 100 ; TraderB: 5 shares @ 10 = 50.
            dbContext.Trades.Add(CreateTrade(TraderA, stockId, 10, 10m, now.AddHours(-1)));
            dbContext.Trades.Add(CreateTrade(TraderB, stockId, 5, 10m, now.AddHours(-3)));

            // --- Trade older than 24h but within 7d: a third user ---
            // TraderC: 20 shares @ 20 = 400, executed 3 days ago.
            dbContext.Trades.Add(CreateTrade(TraderC, stockId, 20, 20m, now.AddDays(-3)));

            // --- Trade older than 7d: must be excluded from both windows ---
            dbContext.Trades.Add(CreateTrade(TraderA, stockId, 99, 99m, now.AddDays(-10)));

            // --- Holdings: two distinct portfolios with qty > 0, one with qty 0 ---
            // totalHeldShares = 7 + 3 = 10  -> marketCap = 10 * CurrentPrice (10) = 100.
            dbContext.Holdings.Add(CreateHolding(portfolioOne.Id, stockId, 7, now));
            dbContext.Holdings.Add(CreateHolding(portfolioTwo.Id, stockId, 3, now));
            dbContext.Holdings.Add(CreateHolding(portfolioEmpty.Id, stockId, 0, now));

            // --- StockPriceHistory within 7d with varying new_price -> stddev of step returns > 0 ---
            // Distinct, monotonically-changing-but-not-constant prices guarantee non-zero variance
            // across the per-step returns, so volatility7d must be strictly positive.
            dbContext.StockPriceHistory.AddRange(
                CreatePriceHistory(stockId, previous: 8m, next: 9m, at: now.AddDays(-5)),
                CreatePriceHistory(stockId, previous: 9m, next: 12m, at: now.AddDays(-4)),
                CreatePriceHistory(stockId, previous: 12m, next: 11m, at: now.AddDays(-2)),
                CreatePriceHistory(stockId, previous: 11m, next: CurrentPrice, at: now.AddHours(-6)));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/v1/market/stocks/{stockId}/analytics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AnalyticsResponse>();
        Assert.NotNull(payload);

        // 24h window reflects only the two last-24h trades (TraderA + TraderB).
        Assert.Equal(15L, payload!.Volume24hShares);
        Assert.Equal(150m, payload.Volume24hValue);

        // 7d window additionally includes the 3-days-ago trade (TraderC), but NOT the 10-days-ago one.
        Assert.Equal(35L, payload.Volume7dShares);
        Assert.Equal(550m, payload.Volume7dValue);

        // Distinct users that traded within 24h: TraderA, TraderB.
        Assert.Equal(2, payload.ActiveTraders24h);

        // Distinct portfolios holding quantity > 0: portfolioOne, portfolioTwo (empty one excluded).
        Assert.Equal(2, payload.OwnershipCount);

        // marketCap = totalHeldShares (7 + 3) * currentPrice (10) = 100.
        Assert.Equal(100m, payload.MarketCap);

        // Varying price history -> strictly positive stddev of step returns.
        Assert.True(payload.Volatility7d > 0m, $"expected volatility7d > 0 but was {payload.Volatility7d}");
    }

    [Fact]
    public async Task GetStockAnalytics_ForUnknownStock_ReturnsNotFound()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        var unknownStockId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/v1/market/stocks/{unknownStockId}/analytics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("NOT_FOUND", error!.Code);
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

    private static Trade CreateTrade(Guid userId, Guid stockId, int quantity, decimal unitPrice, DateTimeOffset executedAt)
    {
        return new Trade
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StockId = stockId,
            TradeType = TradeType.Buy,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalAmount = unitPrice * quantity,
            ExecutedAt = executedAt
        };
    }

    private static Holding CreateHolding(Guid portfolioId, Guid stockId, int quantity, DateTimeOffset at)
    {
        return new Holding
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            StockId = stockId,
            Quantity = quantity,
            AveragePrice = 10m,
            CreatedAt = at,
            CreatedBy = "seed"
        };
    }

    private static StockPriceHistory CreatePriceHistory(Guid stockId, decimal previous, decimal next, DateTimeOffset at)
    {
        return new StockPriceHistory
        {
            Id = Guid.NewGuid(),
            StockId = stockId,
            PreviousPrice = previous,
            NewPrice = next,
            Reason = PriceChangeReason.BuyPressure,
            CreatedAt = at
        };
    }

    private sealed record AnalyticsResponse(
        [property: JsonPropertyName("volume24hShares")] long Volume24hShares,
        [property: JsonPropertyName("volume24hValue")] decimal Volume24hValue,
        [property: JsonPropertyName("volume7dShares")] long Volume7dShares,
        [property: JsonPropertyName("volume7dValue")] decimal Volume7dValue,
        [property: JsonPropertyName("volatility7d")] decimal Volatility7d,
        [property: JsonPropertyName("ownershipCount")] int OwnershipCount,
        [property: JsonPropertyName("activeTraders24h")] int ActiveTraders24h,
        [property: JsonPropertyName("marketCap")] decimal MarketCap);

    private sealed record ErrorResponse(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message);
}
