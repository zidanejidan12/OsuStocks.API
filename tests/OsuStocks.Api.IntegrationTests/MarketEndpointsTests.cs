using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Domain.Models.Market;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class MarketEndpointsTests
{
    [Fact]
    public async Task GetMarket_ReturnsOverview()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var marketRepository = scope.ServiceProvider.GetRequiredService<InMemoryMarketReadRepository>();

        var stockA = new MarketStockDetailsReadModel(Guid.NewGuid(), "mrekk", null, null, 1500m, 1000, 12.5m, null, null);
        var stockB = new MarketStockDetailsReadModel(Guid.NewGuid(), "whitecat", null, null, 1100m, 700, -4.2m, null, null);

        marketRepository.UpsertStock(stockA);
        marketRepository.UpsertStock(stockB);

        var response = await client.GetAsync("/api/v1/market");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MarketOverviewResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.TotalStocks);
        Assert.Equal(1700, payload.TotalVolume);
        Assert.Equal(stockA.StockId, payload.TopGainer.StockId);
        Assert.Equal(stockB.StockId, payload.TopLoser.StockId);
    }

    [Fact]
    public async Task GetMarketStocks_SupportsPagingSearchAndSort()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var marketRepository = scope.ServiceProvider.GetRequiredService<InMemoryMarketReadRepository>();

        marketRepository.UpsertStock(new MarketStockDetailsReadModel(Guid.NewGuid(), "mrekk", null, null, 1500m, 1000, 12m, null, null));
        marketRepository.UpsertStock(new MarketStockDetailsReadModel(Guid.NewGuid(), "aetrna", null, null, 1200m, 400, 2m, null, null));
        marketRepository.UpsertStock(new MarketStockDetailsReadModel(Guid.NewGuid(), "whitecat", null, null, 900m, 800, -5m, null, null));

        var response = await client.GetAsync("/api/v1/market/stocks?page=1&pageSize=2&sort=price_desc&search=a");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MarketStocksPageResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.Page);
        Assert.Equal(2, payload.PageSize);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(2, payload.Items.Count);
        Assert.True(payload.Items[0].CurrentPrice >= payload.Items[1].CurrentPrice);
    }

    [Fact]
    public async Task GetMarketStocks_FiltersByCountry()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var marketRepository = scope.ServiceProvider.GetRequiredService<InMemoryMarketReadRepository>();

        marketRepository.UpsertStock(new MarketStockDetailsReadModel(Guid.NewGuid(), "mrekk", null, "AU", 1500m, 1000, 12m, null, null));
        marketRepository.UpsertStock(new MarketStockDetailsReadModel(Guid.NewGuid(), "aetrna", null, "US", 1200m, 400, 2m, null, null));
        marketRepository.UpsertStock(new MarketStockDetailsReadModel(Guid.NewGuid(), "whitecat", null, "AU", 900m, 800, -5m, null, null));

        // Lower-case to confirm the filter is case-insensitive.
        var response = await client.GetAsync("/api/v1/market/stocks?country=au");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MarketStocksPageResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(2, payload.Items.Count);
        Assert.All(payload.Items, x => Assert.Equal("AU", x.CountryCode));
    }

    [Fact]
    public async Task GetMarketCountries_ReturnsAggregatedCounts()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var marketRepository = scope.ServiceProvider.GetRequiredService<InMemoryMarketReadRepository>();

        marketRepository.UpsertStock(new MarketStockDetailsReadModel(Guid.NewGuid(), "mrekk", null, "AU", 1500m, 1000, 12m, null, null));
        marketRepository.UpsertStock(new MarketStockDetailsReadModel(Guid.NewGuid(), "whitecat", null, "AU", 900m, 800, -5m, null, null));
        marketRepository.UpsertStock(new MarketStockDetailsReadModel(Guid.NewGuid(), "aetrna", null, "US", 1200m, 400, 2m, null, null));
        // Null/empty country codes are excluded from the aggregation.
        marketRepository.UpsertStock(new MarketStockDetailsReadModel(Guid.NewGuid(), "forum", null, null, 800m, 100, 0m, null, null));

        var response = await client.GetAsync("/api/v1/market/countries");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MarketCountriesResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Items.Count);
        // Sorted by count desc, then countryCode asc.
        Assert.Equal("AU", payload.Items[0].CountryCode);
        Assert.Equal(2, payload.Items[0].Count);
        Assert.Equal("US", payload.Items[1].CountryCode);
        Assert.Equal(1, payload.Items[1].Count);
    }

    [Fact]
    public async Task GetMarketStockDetails_ReturnsSingleStock()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var marketRepository = scope.ServiceProvider.GetRequiredService<InMemoryMarketReadRepository>();

        var stock = new MarketStockDetailsReadModel(Guid.NewGuid(), "forum", null, null, 875m, 250, 3.4m, null, null);
        marketRepository.UpsertStock(stock);

        var response = await client.GetAsync($"/api/v1/market/stocks/{stock.StockId}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MarketStockDetailsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(stock.StockId, payload.StockId);
        Assert.Equal("forum", payload.PlayerName);
        Assert.Equal(875m, payload.CurrentPrice);
    }

    [Fact]
    public async Task GetMarketStockHistory_ReturnsTimeSeries()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var marketRepository = scope.ServiceProvider.GetRequiredService<InMemoryMarketReadRepository>();

        var stock = new MarketStockDetailsReadModel(Guid.NewGuid(), "shige", null, null, 1300m, 900, 1.2m, null, null);
        marketRepository.UpsertStock(stock);

        marketRepository.SetHistory(stock.StockId,
        [
            new MarketStockHistoryPointReadModel(DateTimeOffset.UtcNow.AddHours(-2), 1200m),
            new MarketStockHistoryPointReadModel(DateTimeOffset.UtcNow.AddHours(-1), 1250m),
            new MarketStockHistoryPointReadModel(DateTimeOffset.UtcNow, 1300m)
        ]);

        var response = await client.GetAsync($"/api/v1/market/stocks/{stock.StockId}/history");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<MarketStockHistoryPointResponse>>();
        Assert.NotNull(payload);
        Assert.Equal(3, payload.Count);
        Assert.Equal(1200m, payload[0].Price);
        Assert.Equal(1300m, payload[2].Price);
    }

    private sealed record MarketOverviewResponse(
        [property: JsonPropertyName("totalStocks")] int TotalStocks,
        [property: JsonPropertyName("totalVolume")] long TotalVolume,
        [property: JsonPropertyName("topGainer")] TopMoverResponse TopGainer,
        [property: JsonPropertyName("topLoser")] TopMoverResponse TopLoser);

    private sealed record TopMoverResponse(
        [property: JsonPropertyName("stockId")] Guid StockId,
        [property: JsonPropertyName("playerName")] string PlayerName,
        [property: JsonPropertyName("currentPrice")] decimal CurrentPrice,
        [property: JsonPropertyName("priceChange24h")] decimal PriceChange24h);

    private sealed record MarketStocksPageResponse(
        [property: JsonPropertyName("items")] List<MarketStockItemResponse> Items,
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("pageSize")] int PageSize,
        [property: JsonPropertyName("totalCount")] int TotalCount);

    private sealed record MarketStockItemResponse(
        [property: JsonPropertyName("stockId")] Guid StockId,
        [property: JsonPropertyName("playerName")] string PlayerName,
        [property: JsonPropertyName("countryCode")] string? CountryCode,
        [property: JsonPropertyName("currentPrice")] decimal CurrentPrice,
        [property: JsonPropertyName("volume")] long Volume,
        [property: JsonPropertyName("priceChange24h")] decimal PriceChange24h);

    private sealed record MarketCountriesResponse(
        [property: JsonPropertyName("items")] List<MarketCountryItemResponse> Items);

    private sealed record MarketCountryItemResponse(
        [property: JsonPropertyName("countryCode")] string CountryCode,
        [property: JsonPropertyName("count")] int Count);

    private sealed record MarketStockDetailsResponse(
        [property: JsonPropertyName("stockId")] Guid StockId,
        [property: JsonPropertyName("playerName")] string PlayerName,
        [property: JsonPropertyName("currentPrice")] decimal CurrentPrice,
        [property: JsonPropertyName("volume")] long Volume,
        [property: JsonPropertyName("priceChange24h")] decimal PriceChange24h);

    private sealed record MarketStockHistoryPointResponse(
        [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
        [property: JsonPropertyName("price")] decimal Price);
}
