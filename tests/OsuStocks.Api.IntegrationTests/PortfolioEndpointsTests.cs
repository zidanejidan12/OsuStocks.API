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
public sealed class PortfolioEndpointsTests(PostgresTestcontainerFixture fixture)
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetPortfolioSummary_WithHoldings_ReturnsValuationAndProfitLoss()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(TestUserId, 503001));

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
                OsuUserId = 123456,
                Username = "player-123456",
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
                CurrentPrice = 150m,
                DemandScore = 1m,
                PerformanceScore = 1m,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };
            dbContext.PlayerStocks.Add(stock);

            dbContext.Holdings.Add(new Holding
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolio.Id,
                StockId = stock.Id,
                Quantity = 4,
                AveragePrice = 100m,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            });

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/portfolio");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PortfolioSummaryResponse>();
        Assert.NotNull(payload);
        Assert.Equal(600m, payload.CurrentValue);
        Assert.Equal(400m, payload.CostBasis);
        Assert.Equal(200m, payload.ProfitLoss);
        Assert.Single(payload.Holdings);
        Assert.Equal(600m, payload.Holdings[0].CurrentValue);
        Assert.Equal(200m, payload.Holdings[0].ProfitLoss);
    }

    [Fact]
    public async Task GetPortfolioSummary_WithoutPortfolio_ReturnsEmptySummary()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/portfolio");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PortfolioSummaryResponse>();
        Assert.NotNull(payload);
        Assert.Equal(0m, payload.CurrentValue);
        Assert.Equal(0m, payload.CostBasis);
        Assert.Equal(0m, payload.ProfitLoss);
        Assert.Empty(payload.Holdings);
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

    private sealed record PortfolioSummaryResponse(
        [property: JsonPropertyName("currentValue")] decimal CurrentValue,
        [property: JsonPropertyName("costBasis")] decimal CostBasis,
        [property: JsonPropertyName("profitLoss")] decimal ProfitLoss,
        [property: JsonPropertyName("holdings")] List<PortfolioHoldingResponse> Holdings);

    private sealed record PortfolioHoldingResponse(
        [property: JsonPropertyName("stockId")] Guid StockId,
        [property: JsonPropertyName("quantity")] int Quantity,
        [property: JsonPropertyName("averagePrice")] decimal AveragePrice,
        [property: JsonPropertyName("currentPrice")] decimal CurrentPrice,
        [property: JsonPropertyName("costBasis")] decimal CostBasis,
        [property: JsonPropertyName("currentValue")] decimal CurrentValue,
        [property: JsonPropertyName("profitLoss")] decimal ProfitLoss);
}
