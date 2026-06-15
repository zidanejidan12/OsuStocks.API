using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Models.Market;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class MarketMaintenanceModeEndpointsTests
{
    [Fact]
    public async Task TradingEndpoints_WhenMaintenanceModeEnabled_ReturnConflict()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var marketSettingsRepository = scope.ServiceProvider.GetRequiredService<InMemoryMarketSettingsRepository>();
            marketSettingsRepository.Seed(new MarketSettings
            {
                Id = Guid.NewGuid(),
                PpMultiplier = 1m,
                TradeMultiplier = 1m,
                DecayMultiplier = 1m,
                IsMaintenanceMode = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            });
        }

        var buyResponse = await client.PostAsJsonAsync(
            "/api/v1/trading/buy",
            new TradeRequest(Guid.NewGuid(), 1));

        Assert.Equal(HttpStatusCode.Conflict, buyResponse.StatusCode);

        var buyError = await buyResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(buyError);
        Assert.Equal("CONFLICT", buyError.Code);

        var sellResponse = await client.PostAsJsonAsync(
            "/api/v1/trading/sell",
            new TradeRequest(Guid.NewGuid(), 1));

        Assert.Equal(HttpStatusCode.Conflict, sellResponse.StatusCode);

        var sellError = await sellResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(sellError);
        Assert.Equal("CONFLICT", sellError.Code);
    }

    [Fact]
    public async Task MarketReadEndpoints_WhenMaintenanceModeEnabled_StillReturnSuccess()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var marketSettingsRepository = scope.ServiceProvider.GetRequiredService<InMemoryMarketSettingsRepository>();
            marketSettingsRepository.Seed(new MarketSettings
            {
                Id = Guid.NewGuid(),
                PpMultiplier = 1m,
                TradeMultiplier = 1m,
                DecayMultiplier = 1m,
                IsMaintenanceMode = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            });

            var marketReadRepository = scope.ServiceProvider.GetRequiredService<InMemoryMarketReadRepository>();
            marketReadRepository.UpsertStock(new MarketStockDetailsReadModel(Guid.NewGuid(), "maintenance-player", null, null, 100m, 42, 1.5m, null, null));
        }

        var response = await client.GetAsync("/api/v1/market");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MarketOverviewResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.TotalStocks);
    }

    private sealed record TradeRequest(Guid StockId, int Quantity);

    private sealed record ErrorResponse(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("traceId")] string TraceId);

    private sealed record MarketOverviewResponse(
        [property: JsonPropertyName("totalStocks")] int TotalStocks,
        [property: JsonPropertyName("totalVolume")] long TotalVolume);
}
