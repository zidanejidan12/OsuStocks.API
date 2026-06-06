using OsuStocks.Api.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class AdminMarketSettingsEndpointsTests
{
    [Fact]
    public async Task GetMarketSettings_WhenMissing_ReturnsDefaults()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/market-settings");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MarketSettingsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1m, payload.PpMultiplier);
        Assert.Equal(1m, payload.TradeMultiplier);
        Assert.Equal(1m, payload.DecayMultiplier);
        Assert.False(payload.IsMaintenanceMode);
    }

    [Fact]
    public async Task PutMarketSettings_ThenGet_ReturnsUpdatedValues()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var updateResponse = await client.PutAsJsonAsync(
            "/api/v1/admin/market-settings",
            new UpdateMarketSettingsRequest(1.5m, 1.2m, 0.5m, true));

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/v1/admin/market-settings");
        getResponse.EnsureSuccessStatusCode();

        var payload = await getResponse.Content.ReadFromJsonAsync<MarketSettingsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1.5m, payload.PpMultiplier);
        Assert.Equal(1.2m, payload.TradeMultiplier);
        Assert.Equal(0.5m, payload.DecayMultiplier);
        Assert.True(payload.IsMaintenanceMode);
    }

    [Fact]
    public async Task PutMarketSettings_WithInvalidRange_ReturnsBadRequest()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/v1/admin/market-settings",
            new UpdateMarketSettingsRequest(-0.1m, 1m, 1m, false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("VALIDATION_ERROR", payload.Code);
    }

    private sealed record UpdateMarketSettingsRequest(decimal PpMultiplier, decimal TradeMultiplier, decimal DecayMultiplier, bool IsMaintenanceMode);

    private sealed record MarketSettingsResponse(
        [property: JsonPropertyName("ppMultiplier")] decimal PpMultiplier,
        [property: JsonPropertyName("tradeMultiplier")] decimal TradeMultiplier,
        [property: JsonPropertyName("decayMultiplier")] decimal DecayMultiplier,
        [property: JsonPropertyName("isMaintenanceMode")] bool IsMaintenanceMode);

    private sealed record ErrorResponse(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("traceId")] string TraceId);
}
