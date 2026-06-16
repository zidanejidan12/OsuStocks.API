using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Infrastructure.Persistence;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class ProjectedReadModelsQueryCountTests(PostgresTestcontainerFixture fixture)
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task PortfolioSummary_UsesSingleSelectQuery()
    {
        var queryCounter = new QueryCountingCommandInterceptor();
        await using var factory = new PostgresWebApplicationFactory(fixture, queryCounter);
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(TestUserId, 700001));

            var portfolio = new Portfolio
            {
                Id = Guid.NewGuid(),
                UserId = TestUserId,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };

            dbContext.Portfolios.Add(portfolio);

            for (var i = 0; i < 3; i++)
            {
                var trackedPlayer = new TrackedPlayer
                {
                    Id = Guid.NewGuid(),
                    OsuUserId = 800000 + i,
                    Username = $"player-{800000 + i}",
                    TrackingTier = TrackingTier.Tier2,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "seed"
                };

                var stock = new PlayerStock
                {
                    Id = Guid.NewGuid(),
                    TrackedPlayerId = trackedPlayer.Id,
                    CurrentPrice = 100m + (i * 10m),
                    DemandScore = 1m,
                    PerformanceScore = 1m,
                    LastUpdated = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "seed"
                };

                dbContext.TrackedPlayers.Add(trackedPlayer);
                dbContext.PlayerStocks.Add(stock);
                dbContext.Holdings.Add(new Holding
                {
                    Id = Guid.NewGuid(),
                    PortfolioId = portfolio.Id,
                    StockId = stock.Id,
                    Quantity = 2 + i,
                    AveragePrice = 80m + (i * 10m),
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "seed"
                });
            }

            await dbContext.SaveChangesAsync();
        }

        queryCounter.Reset();

        var response = await client.GetAsync("/api/v1/portfolio");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PortfolioSummaryEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal(3, payload.Holdings.Count);
        Assert.Equal(1, queryCounter.SelectCommandCount);
    }

    [Fact]
    public async Task Holdings_UsesSingleSelectQuery()
    {
        var queryCounter = new QueryCountingCommandInterceptor();
        await using var factory = new PostgresWebApplicationFactory(fixture, queryCounter);
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(TestUserId, 700002));

            var portfolio = new Portfolio
            {
                Id = Guid.NewGuid(),
                UserId = TestUserId,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };

            dbContext.Portfolios.Add(portfolio);

            for (var i = 0; i < 2; i++)
            {
                var trackedPlayer = new TrackedPlayer
                {
                    Id = Guid.NewGuid(),
                    OsuUserId = 810000 + i,
                    Username = $"player-{810000 + i}",
                    TrackingTier = TrackingTier.Tier3,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "seed"
                };

                var stock = new PlayerStock
                {
                    Id = Guid.NewGuid(),
                    TrackedPlayerId = trackedPlayer.Id,
                    CurrentPrice = 200m + (i * 50m),
                    DemandScore = 1m,
                    PerformanceScore = 1m,
                    LastUpdated = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "seed"
                };

                dbContext.TrackedPlayers.Add(trackedPlayer);
                dbContext.PlayerStocks.Add(stock);
                dbContext.Holdings.Add(new Holding
                {
                    Id = Guid.NewGuid(),
                    PortfolioId = portfolio.Id,
                    StockId = stock.Id,
                    Quantity = 1 + i,
                    AveragePrice = 150m + (i * 10m),
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "seed"
                });
            }

            await dbContext.SaveChangesAsync();
        }

        queryCounter.Reset();

        var response = await client.GetAsync("/api/v1/portfolio/holdings");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<HoldingsEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal(1, queryCounter.SelectCommandCount);
    }

    [Fact]
    public async Task TradeHistory_UsesSingleSelectQuery()
    {
        var queryCounter = new QueryCountingCommandInterceptor();
        await using var factory = new PostgresWebApplicationFactory(fixture, queryCounter);
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(TestUserId, 700003));

            for (var i = 0; i < 5; i++)
            {
                var trackedPlayer = new TrackedPlayer
                {
                    Id = Guid.NewGuid(),
                    OsuUserId = 820000 + i,
                    Username = $"player-{820000 + i}",
                    TrackingTier = TrackingTier.Tier1,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "seed"
                };

                var stock = new PlayerStock
                {
                    Id = Guid.NewGuid(),
                    TrackedPlayerId = trackedPlayer.Id,
                    CurrentPrice = 300m + i,
                    DemandScore = 1m,
                    PerformanceScore = 1m,
                    LastUpdated = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "seed"
                };

                dbContext.TrackedPlayers.Add(trackedPlayer);
                dbContext.PlayerStocks.Add(stock);
                dbContext.Trades.Add(new Trade
                {
                    Id = Guid.NewGuid(),
                    UserId = TestUserId,
                    StockId = stock.Id,
                    TradeType = i % 2 == 0 ? TradeType.Buy : TradeType.Sell,
                    Quantity = 10 + i,
                    UnitPrice = 20m + i,
                    TotalAmount = (20m + i) * (10 + i),
                    ExecutedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
                });
            }

            await dbContext.SaveChangesAsync();
        }

        queryCounter.Reset();

        var response = await client.GetAsync("/api/v1/trading/history?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TradeHistoryEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal(5, payload.Items.Count);
        Assert.Equal(1, queryCounter.SelectCommandCount);
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

    private sealed record PortfolioSummaryEnvelope(
        [property: JsonPropertyName("holdings")] List<PortfolioHoldingEnvelope> Holdings);

    private sealed record PortfolioHoldingEnvelope(
        [property: JsonPropertyName("holdingId")] Guid HoldingId,
        [property: JsonPropertyName("stockId")] Guid StockId,
        [property: JsonPropertyName("playerName")] string? PlayerName,
        [property: JsonPropertyName("quantity")] decimal Quantity,
        [property: JsonPropertyName("averagePrice")] decimal AveragePrice,
        [property: JsonPropertyName("currentPrice")] decimal CurrentPrice,
        [property: JsonPropertyName("costBasis")] decimal CostBasis,
        [property: JsonPropertyName("currentValue")] decimal CurrentValue,
        [property: JsonPropertyName("profitLoss")] decimal ProfitLoss);

    private sealed record HoldingsEnvelope([property: JsonPropertyName("items")] List<HoldingEnvelope> Items);

    private sealed record HoldingEnvelope(
        [property: JsonPropertyName("holdingId")] Guid HoldingId,
        [property: JsonPropertyName("stockId")] Guid StockId,
        [property: JsonPropertyName("playerName")] string? PlayerName,
        [property: JsonPropertyName("quantity")] decimal Quantity,
        [property: JsonPropertyName("averagePrice")] decimal AveragePrice,
        [property: JsonPropertyName("currentPrice")] decimal CurrentPrice);

    private sealed record TradeHistoryEnvelope([property: JsonPropertyName("items")] List<TradeHistoryItemEnvelope> Items);

    private sealed record TradeHistoryItemEnvelope(
        [property: JsonPropertyName("tradeId")] Guid TradeId,
        [property: JsonPropertyName("stockId")] Guid StockId,
        [property: JsonPropertyName("tradeType")] string TradeType,
        [property: JsonPropertyName("quantity")] decimal Quantity,
        [property: JsonPropertyName("unitPrice")] decimal UnitPrice,
        [property: JsonPropertyName("totalAmount")] decimal TotalAmount,
        [property: JsonPropertyName("executedAt")] DateTimeOffset ExecutedAt,
        [property: JsonPropertyName("playerName")] string? PlayerName);
}
