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
public sealed class MarketTrendingEndpointTests(PostgresTestcontainerFixture fixture)
{
    // A single trading user is sufficient: trending metrics are global per-stock, not per-user.
    // The factory's TestAuthHandler authenticates the request; this user only owns the seeded trades.
    private static readonly Guid Trader = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    [Fact]
    public async Task GetTrending_RanksSections_BySharesVolumeAndPriceMovement()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        // Stock X: heavy Buy quantity, rising price (baseline < current).
        // Stock Y: heavy Sell quantity, falling price (baseline > current).
        // Stock Z: highest credit volume (SUM total_amount) via high unit prices, neutral movement.
        Guid stockXId;
        Guid stockYId;
        Guid stockZId;

        var now = DateTimeOffset.UtcNow;
        // Any row whose CreatedAt is at-or-before this start is treated as the baseline.
        var windowStart = now.AddHours(-24);
        var beforeWindow = windowStart.AddHours(-2);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(Trader, 950001));

            // --- Stock X: current 15, baseline 10 -> rising (+50%). ---
            var (playerX, stockX) = CreateStock(
                dbContext, osuUserId: 950100, username: "stock-x", currentPrice: 15m, now);
            stockXId = stockX.Id;

            // --- Stock Y: current 8, baseline 10 -> falling (-20%). ---
            var (playerY, stockY) = CreateStock(
                dbContext, osuUserId: 950200, username: "stock-y", currentPrice: 8m, now);
            stockYId = stockY.Id;

            // --- Stock Z: current 100, baseline 100 -> flat; dominates highestVolume. ---
            var (playerZ, stockZ) = CreateStock(
                dbContext, osuUserId: 950300, username: "stock-z", currentPrice: 100m, now);
            stockZId = stockZ.Id;

            _ = (playerX, playerY, playerZ);

            // --- Baseline price-history rows older than the window start. ---
            // The repository's baseline = latest history NewPrice with CreatedAt <= windowStart.
            dbContext.StockPriceHistory.Add(CreatePriceHistory(stockXId, previous: 9m, newPrice: 10m, at: beforeWindow));
            dbContext.StockPriceHistory.Add(CreatePriceHistory(stockYId, previous: 11m, newPrice: 10m, at: beforeWindow));
            dbContext.StockPriceHistory.Add(CreatePriceHistory(stockZId, previous: 100m, newPrice: 100m, at: beforeWindow));

            // --- Trades within the last 24h. ---
            // Stock X: Buy 100 shares (top mostBought). Total credit = 100 * 15 = 1500.
            dbContext.Trades.Add(CreateTrade(Trader, stockXId, TradeType.Buy, quantity: 100, unitPrice: 15m, now.AddHours(-2)));
            // Stock X also a small sell so it appears but does not lead mostSold.
            dbContext.Trades.Add(CreateTrade(Trader, stockXId, TradeType.Sell, quantity: 1, unitPrice: 15m, now.AddHours(-2)));

            // Stock Y: Sell 80 shares (top mostSold). Total credit = 80 * 8 = 640.
            dbContext.Trades.Add(CreateTrade(Trader, stockYId, TradeType.Sell, quantity: 80, unitPrice: 8m, now.AddHours(-3)));
            // Stock Y also a small buy so it appears but does not lead mostBought.
            dbContext.Trades.Add(CreateTrade(Trader, stockYId, TradeType.Buy, quantity: 2, unitPrice: 8m, now.AddHours(-3)));

            // Stock Z: modest share counts but very high unit price -> highest credit volume.
            // Total credit = 50 * 100 = 5000 (> 1500 and > 640).
            dbContext.Trades.Add(CreateTrade(Trader, stockZId, TradeType.Buy, quantity: 50, unitPrice: 100m, now.AddHours(-1)));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/market/trending?window=24h&limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TrendingResponse>();
        Assert.NotNull(payload);
        Assert.NotNull(payload!.MostBought);
        Assert.NotNull(payload.MostSold);
        Assert.NotNull(payload.FastestRising);
        Assert.NotNull(payload.FastestFalling);
        Assert.NotNull(payload.HighestVolume);

        // --- mostBought: Stock X leads with SUM(buy quantity) = 100. ---
        Assert.NotEmpty(payload.MostBought);
        Assert.Equal(stockXId, payload.MostBought[0].StockId);
        Assert.Equal("stock-x", payload.MostBought[0].PlayerName);
        Assert.Equal(100m, payload.MostBought[0].MetricValue);
        Assert.Equal(15m, payload.MostBought[0].CurrentPrice);

        // --- mostSold: Stock Y leads with SUM(sell quantity) = 80. ---
        Assert.NotEmpty(payload.MostSold);
        Assert.Equal(stockYId, payload.MostSold[0].StockId);
        Assert.Equal("stock-y", payload.MostSold[0].PlayerName);
        Assert.Equal(80m, payload.MostSold[0].MetricValue);
        Assert.Equal(8m, payload.MostSold[0].CurrentPrice);

        // --- highestVolume: Stock Z leads with SUM(total_amount) = 5000. ---
        Assert.NotEmpty(payload.HighestVolume);
        Assert.Equal(stockZId, payload.HighestVolume[0].StockId);
        Assert.Equal("stock-z", payload.HighestVolume[0].PlayerName);
        Assert.Equal(5000m, payload.HighestVolume[0].MetricValue);
        Assert.Equal(100m, payload.HighestVolume[0].CurrentPrice);

        // --- fastestRising: Stock X present with positive percent change. ---
        var risingX = Assert.Single(payload.FastestRising, x => x.StockId == stockXId);
        Assert.True(
            risingX.MetricValue > 0m,
            $"expected Stock X rising metric > 0 but was {risingX.MetricValue}");
        Assert.Equal(15m, risingX.CurrentPrice);
        // Stock X has the strongest positive movement among the seeded set, so it tops fastestRising.
        Assert.Equal(stockXId, payload.FastestRising[0].StockId);

        // --- fastestFalling: Stock Y present with negative percent change. ---
        var fallingY = Assert.Single(payload.FastestFalling, x => x.StockId == stockYId);
        Assert.True(
            fallingY.MetricValue < 0m,
            $"expected Stock Y falling metric < 0 but was {fallingY.MetricValue}");
        Assert.Equal(8m, fallingY.CurrentPrice);
        // Stock Y is the only stock with negative movement, so it tops fastestFalling.
        Assert.Equal(stockYId, payload.FastestFalling[0].StockId);
    }

    [Fact]
    public async Task GetTrending_CapsEachSection_ToLimit()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        const int limit = 2;
        const int stockCount = 5;

        var now = DateTimeOffset.UtcNow;
        var beforeWindow = now.AddHours(-26);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(Trader, 960001));

            for (var i = 0; i < stockCount; i++)
            {
                var (_, stock) = CreateStock(
                    dbContext,
                    osuUserId: 960100 + i,
                    username: $"limit-stock-{i}",
                    currentPrice: 10m + i,
                    now);

                // Baseline below current -> every stock is rising (eligible for fastestRising).
                dbContext.StockPriceHistory.Add(
                    CreatePriceHistory(stock.Id, previous: 4m, newPrice: 5m, at: beforeWindow));

                // Each stock has both buys and sells, and a credit volume, within the window.
                dbContext.Trades.Add(CreateTrade(Trader, stock.Id, TradeType.Buy, quantity: 10 + i, unitPrice: 10m, now.AddHours(-1)));
                dbContext.Trades.Add(CreateTrade(Trader, stock.Id, TradeType.Sell, quantity: 5 + i, unitPrice: 10m, now.AddHours(-1)));
            }

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/v1/market/trending?window=24h&limit={limit}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TrendingResponse>();
        Assert.NotNull(payload);

        // Each section must be capped at `limit`, even though stockCount > limit qualify.
        Assert.Equal(limit, payload!.MostBought.Count);
        Assert.Equal(limit, payload.MostSold.Count);
        Assert.Equal(limit, payload.FastestRising.Count);
        Assert.Equal(limit, payload.FastestFalling.Count);
        Assert.Equal(limit, payload.HighestVolume.Count);
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

    private static (TrackedPlayer Player, PlayerStock Stock) CreateStock(
        AppDbContext dbContext,
        long osuUserId,
        string username,
        decimal currentPrice,
        DateTimeOffset now)
    {
        var player = new TrackedPlayer
        {
            Id = Guid.NewGuid(),
            OsuUserId = osuUserId,
            Username = username,
            TrackingTier = TrackingTier.Tier1,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "seed"
        };
        dbContext.TrackedPlayers.Add(player);

        var stock = new PlayerStock
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = player.Id,
            CurrentPrice = currentPrice,
            DemandScore = 1m,
            PerformanceScore = 1m,
            CreatedAt = now,
            LastUpdated = now,
            CreatedBy = "seed"
        };
        dbContext.PlayerStocks.Add(stock);

        return (player, stock);
    }

    private static Trade CreateTrade(
        Guid userId,
        Guid stockId,
        TradeType tradeType,
        int quantity,
        decimal unitPrice,
        DateTimeOffset executedAt)
    {
        return new Trade
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StockId = stockId,
            TradeType = tradeType,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalAmount = unitPrice * quantity,
            ExecutedAt = executedAt
        };
    }

    private static StockPriceHistory CreatePriceHistory(
        Guid stockId,
        decimal previous,
        decimal newPrice,
        DateTimeOffset at)
    {
        return new StockPriceHistory
        {
            Id = Guid.NewGuid(),
            StockId = stockId,
            PreviousPrice = previous,
            NewPrice = newPrice,
            Reason = PriceChangeReason.BuyPressure,
            CreatedAt = at
        };
    }

    private sealed record TrendingResponse(
        [property: JsonPropertyName("mostBought")] IReadOnlyList<TrendingStock> MostBought,
        [property: JsonPropertyName("mostSold")] IReadOnlyList<TrendingStock> MostSold,
        [property: JsonPropertyName("fastestRising")] IReadOnlyList<TrendingStock> FastestRising,
        [property: JsonPropertyName("fastestFalling")] IReadOnlyList<TrendingStock> FastestFalling,
        [property: JsonPropertyName("highestVolume")] IReadOnlyList<TrendingStock> HighestVolume);

    private sealed record TrendingStock(
        [property: JsonPropertyName("stockId")] Guid StockId,
        [property: JsonPropertyName("playerName")] string PlayerName,
        [property: JsonPropertyName("metricValue")] decimal MetricValue,
        [property: JsonPropertyName("currentPrice")] decimal CurrentPrice);
}
