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
public sealed class MarketActivityFeedEndpointsTests(PostgresTestcontainerFixture fixture)
{
    // A fixed instant the seeded CreatedAt values are derived from, so ordering is deterministic.
    private static readonly DateTimeOffset BaseTime =
        new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetMarketEvents_ReturnsFeedOrderedByOccurredAtDesc_WithDeterministicPercentChange()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid stockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var seed = SeedStock(dbContext, osuUserId: 910001, username: "mrekk", currentPrice: 104m);
            stockId = seed.StockId;

            // Three history rows with distinct reasons, deterministic percent change, increasing CreatedAt.
            // prev=100,new=104 -> +4.00 ; prev=100,new=98.5 -> -1.50 ; prev=100,new=110 -> +10.00
            AddHistory(dbContext, stockId, 100m, 104m, PriceChangeReason.BuyPressure, BaseTime.AddMinutes(1));
            AddHistory(dbContext, stockId, 100m, 98.5m, PriceChangeReason.Decay, BaseTime.AddMinutes(2));
            AddHistory(dbContext, stockId, 100m, 110m, PriceChangeReason.TopPlay, BaseTime.AddMinutes(3));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/market/events");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<FeedEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal(3, payload.Items.Count);

        // Ordered by occurredAt descending: TopPlay (t+3), Decay (t+2), BuyPressure (t+1).
        Assert.Equal("TopPlay", payload.Items[0].Reason);
        Assert.Equal("Decay", payload.Items[1].Reason);
        Assert.Equal("BuyPressure", payload.Items[2].Reason);

        Assert.True(
            payload.Items[0].OccurredAt > payload.Items[1].OccurredAt,
            "Items should be ordered by occurredAt descending.");
        Assert.True(
            payload.Items[1].OccurredAt > payload.Items[2].OccurredAt,
            "Items should be ordered by occurredAt descending.");

        // Each item is fully populated.
        var topPlay = payload.Items[0];
        Assert.Equal(stockId, topPlay.StockId);
        Assert.Equal("mrekk", topPlay.PlayerName);
        Assert.Equal("TopPlay", topPlay.Reason);
        Assert.Equal("Top play detected", topPlay.Description);
        Assert.Equal(10.00m, topPlay.PercentChange);
        Assert.Equal(110m, topPlay.NewPrice);

        var decay = payload.Items[1];
        Assert.Equal("Decay", decay.Reason);
        Assert.Equal("Inactivity decay", decay.Description);
        Assert.Equal(-1.50m, decay.PercentChange);
        Assert.Equal(98.5m, decay.NewPrice);

        var buyPressure = payload.Items[2];
        Assert.Equal("BuyPressure", buyPressure.Reason);
        Assert.Equal("Heavy buy pressure", buyPressure.Description);
        Assert.Equal(4.00m, buyPressure.PercentChange);
        Assert.Equal(104m, buyPressure.NewPrice);
    }

    [Fact]
    public async Task GetMarketEvents_SpansMultipleStocks()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid firstStockId;
        Guid secondStockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var first = SeedStock(dbContext, osuUserId: 910010, username: "first-player", currentPrice: 104m);
            var second = SeedStock(dbContext, osuUserId: 910011, username: "second-player", currentPrice: 110m);
            firstStockId = first.StockId;
            secondStockId = second.StockId;

            AddHistory(dbContext, firstStockId, 100m, 104m, PriceChangeReason.BuyPressure, BaseTime.AddMinutes(1));
            AddHistory(dbContext, secondStockId, 100m, 110m, PriceChangeReason.TopPlay, BaseTime.AddMinutes(2));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/market/events");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<FeedEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);

        // The global feed includes rows from both stocks, newest first.
        Assert.Equal(secondStockId, payload.Items[0].StockId);
        Assert.Equal("second-player", payload.Items[0].PlayerName);
        Assert.Equal(firstStockId, payload.Items[1].StockId);
        Assert.Equal("first-player", payload.Items[1].PlayerName);
    }

    [Fact]
    public async Task GetMarketEvents_WithTypeFilter_ReturnsOnlyMatchingReason()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid stockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var seed = SeedStock(dbContext, osuUserId: 910020, username: "filter-player", currentPrice: 104m);
            stockId = seed.StockId;

            AddHistory(dbContext, stockId, 100m, 104m, PriceChangeReason.BuyPressure, BaseTime.AddMinutes(1));
            AddHistory(dbContext, stockId, 100m, 98.5m, PriceChangeReason.Decay, BaseTime.AddMinutes(2));
            AddHistory(dbContext, stockId, 100m, 106m, PriceChangeReason.BuyPressure, BaseTime.AddMinutes(3));
            AddHistory(dbContext, stockId, 100m, 110m, PriceChangeReason.TopPlay, BaseTime.AddMinutes(4));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/market/events?type=BuyPressure");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<FeedEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);
        Assert.All(payload.Items, item => Assert.Equal("BuyPressure", item.Reason));

        // Still ordered by occurredAt descending within the filtered set (t+3 before t+1).
        Assert.Equal(106m, payload.Items[0].NewPrice);
        Assert.Equal(104m, payload.Items[1].NewPrice);
    }

    [Fact]
    public async Task GetMarketEvents_WithPaging_LimitsResults()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid stockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var seed = SeedStock(dbContext, osuUserId: 910030, username: "paging-player", currentPrice: 104m);
            stockId = seed.StockId;

            // Five rows with strictly increasing CreatedAt and increasing NewPrice so we can
            // identify exactly which rows each page returns.
            for (var i = 1; i <= 5; i++)
            {
                AddHistory(dbContext, stockId, 100m, 100m + i, PriceChangeReason.BuyPressure, BaseTime.AddMinutes(i));
            }

            await dbContext.SaveChangesAsync();
        }

        var firstPage = await client.GetAsync("/api/v1/market/events?page=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);

        var firstPayload = await firstPage.Content.ReadFromJsonAsync<FeedEnvelope>();
        Assert.NotNull(firstPayload);
        Assert.Equal(2, firstPayload.Items.Count);
        Assert.Equal(1, firstPayload.Page);
        Assert.Equal(2, firstPayload.PageSize);
        // Newest first: t+5 (105) then t+4 (104).
        Assert.Equal(105m, firstPayload.Items[0].NewPrice);
        Assert.Equal(104m, firstPayload.Items[1].NewPrice);

        var secondPage = await client.GetAsync("/api/v1/market/events?page=2&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);

        var secondPayload = await secondPage.Content.ReadFromJsonAsync<FeedEnvelope>();
        Assert.NotNull(secondPayload);
        Assert.Equal(2, secondPayload.Items.Count);
        Assert.Equal(2, secondPayload.Page);
        // Continuing newest-first: t+3 (103) then t+2 (102).
        Assert.Equal(103m, secondPayload.Items[0].NewPrice);
        Assert.Equal(102m, secondPayload.Items[1].NewPrice);
    }

    [Fact]
    public async Task GetStockEvents_ReturnsOnlyThatStocksRows_OrderedDesc()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid targetStockId;
        Guid otherStockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var target = SeedStock(dbContext, osuUserId: 910040, username: "target-player", currentPrice: 104m);
            var other = SeedStock(dbContext, osuUserId: 910041, username: "other-player", currentPrice: 110m);
            targetStockId = target.StockId;
            otherStockId = other.StockId;

            // Target stock: two rows.
            AddHistory(dbContext, targetStockId, 100m, 104m, PriceChangeReason.BuyPressure, BaseTime.AddMinutes(1));
            AddHistory(dbContext, targetStockId, 100m, 98.5m, PriceChangeReason.Decay, BaseTime.AddMinutes(3));

            // Other stock: one row that must be excluded from the per-stock feed.
            AddHistory(dbContext, otherStockId, 100m, 110m, PriceChangeReason.TopPlay, BaseTime.AddMinutes(2));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/v1/market/events/{targetStockId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<FeedEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);

        // Only the target stock's rows are present.
        Assert.All(payload.Items, item => Assert.Equal(targetStockId, item.StockId));
        Assert.DoesNotContain(payload.Items, item => item.StockId == otherStockId);

        // Ordered by occurredAt descending: Decay (t+3) before BuyPressure (t+1).
        Assert.Equal("Decay", payload.Items[0].Reason);
        Assert.Equal(-1.50m, payload.Items[0].PercentChange);
        Assert.Equal(98.5m, payload.Items[0].NewPrice);
        Assert.Equal("target-player", payload.Items[0].PlayerName);

        Assert.Equal("BuyPressure", payload.Items[1].Reason);
        Assert.Equal(4.00m, payload.Items[1].PercentChange);
        Assert.Equal(104m, payload.Items[1].NewPrice);

        Assert.True(
            payload.Items[0].OccurredAt > payload.Items[1].OccurredAt,
            "Per-stock items should be ordered by occurredAt descending.");
    }

    private static (Guid TrackedPlayerId, Guid StockId) SeedStock(
        AppDbContext dbContext,
        long osuUserId,
        string username,
        decimal currentPrice)
    {
        var trackedPlayer = new TrackedPlayer
        {
            Id = Guid.NewGuid(),
            OsuUserId = osuUserId,
            Username = username,
            TrackingTier = TrackingTier.Tier1,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        };
        dbContext.TrackedPlayers.Add(trackedPlayer);

        var stock = new PlayerStock
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = trackedPlayer.Id,
            CurrentPrice = currentPrice,
            DemandScore = 1m,
            PerformanceScore = 1m,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        };
        dbContext.PlayerStocks.Add(stock);

        return (trackedPlayer.Id, stock.Id);
    }

    private static void AddHistory(
        AppDbContext dbContext,
        Guid stockId,
        decimal previousPrice,
        decimal newPrice,
        PriceChangeReason reason,
        DateTimeOffset createdAt)
    {
        dbContext.StockPriceHistory.Add(new StockPriceHistory
        {
            Id = Guid.NewGuid(),
            StockId = stockId,
            PreviousPrice = previousPrice,
            NewPrice = newPrice,
            Reason = reason,
            CreatedAt = createdAt
        });
    }

    private sealed record FeedEnvelope(
        [property: JsonPropertyName("items")] List<FeedItem> Items,
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("pageSize")] int PageSize);

    private sealed record FeedItem(
        [property: JsonPropertyName("stockId")] Guid StockId,
        [property: JsonPropertyName("playerName")] string PlayerName,
        [property: JsonPropertyName("reason")] string Reason,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("percentChange")] decimal PercentChange,
        [property: JsonPropertyName("newPrice")] decimal NewPrice,
        [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt);
}
