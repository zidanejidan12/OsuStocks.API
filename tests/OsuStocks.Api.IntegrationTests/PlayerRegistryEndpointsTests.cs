using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class PlayerRegistryEndpointsTests
{
    [Fact]
    public async Task SearchOsuPlayer_ReturnsCandidatesWithTrackingState()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/tracked-players/search?query=mre&limit=10");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SearchOsuPlayersEnvelope>();

        Assert.NotNull(payload);
        Assert.Contains(payload.Items, item => item.OsuUserId == 1001 && !item.IsTracked);
    }

    [Fact]
    public async Task AddTrackedPlayer_ThenList_ReturnsAddedPlayer_AndCreatesStock()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var addResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/tracked-players",
            new AddTrackedPlayerRequest(1001, 1));

        addResponse.EnsureSuccessStatusCode();

        var added = await addResponse.Content.ReadFromJsonAsync<AddTrackedPlayerEnvelope>();

        Assert.NotNull(added);
        Assert.NotEqual(Guid.Empty, added.TrackedPlayerId);

        var listResponse = await client.GetAsync("/api/v1/admin/tracked-players");
        listResponse.EnsureSuccessStatusCode();

        var listPayload = await listResponse.Content.ReadFromJsonAsync<ListTrackedPlayersEnvelope>();

        Assert.NotNull(listPayload);
        Assert.Contains(listPayload.Items, item => item.TrackedPlayerId == added.TrackedPlayerId && item.IsActive);

        using var scope = factory.Services.CreateScope();
        var stockRepository = scope.ServiceProvider.GetRequiredService<InMemoryPlayerStockRepository>();
        var stock = await stockRepository.GetByTrackedPlayerIdAsync(added.TrackedPlayerId);

        Assert.NotNull(stock);
        Assert.Equal(added.TrackedPlayerId, stock.TrackedPlayerId);
        Assert.True(stock.CurrentPrice > 0m);
    }

    [Fact]
    public async Task DisableThenEnableTrackedPlayer_UpdatesActiveState()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var addResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/tracked-players",
            new AddTrackedPlayerRequest(1002, 2));

        addResponse.EnsureSuccessStatusCode();
        var added = await addResponse.Content.ReadFromJsonAsync<AddTrackedPlayerEnvelope>();

        Assert.NotNull(added);

        var disableResponse = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/admin/tracked-players/{added.TrackedPlayerId}/disable"));

        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        var inactiveListResponse = await client.GetAsync("/api/v1/admin/tracked-players?isActive=false");
        inactiveListResponse.EnsureSuccessStatusCode();

        var inactiveList = await inactiveListResponse.Content.ReadFromJsonAsync<ListTrackedPlayersEnvelope>();

        Assert.NotNull(inactiveList);
        Assert.Contains(inactiveList.Items, item => item.TrackedPlayerId == added.TrackedPlayerId && !item.IsActive);

        var enableResponse = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/admin/tracked-players/{added.TrackedPlayerId}/enable"));

        Assert.Equal(HttpStatusCode.NoContent, enableResponse.StatusCode);

        var activeListResponse = await client.GetAsync("/api/v1/admin/tracked-players?isActive=true");
        activeListResponse.EnsureSuccessStatusCode();

        var activeList = await activeListResponse.Content.ReadFromJsonAsync<ListTrackedPlayersEnvelope>();

        Assert.NotNull(activeList);
        Assert.Contains(activeList.Items, item => item.TrackedPlayerId == added.TrackedPlayerId && item.IsActive);
    }

    private sealed record AddTrackedPlayerRequest(long OsuUserId, int TrackingTier);

    private sealed record AddTrackedPlayerEnvelope(
        [property: JsonPropertyName("trackedPlayerId")] Guid TrackedPlayerId);

    private sealed record SearchOsuPlayersEnvelope(
        [property: JsonPropertyName("items")] List<SearchOsuPlayerItem> Items);

    private sealed record SearchOsuPlayerItem(
        [property: JsonPropertyName("osuUserId")] long OsuUserId,
        [property: JsonPropertyName("isTracked")] bool IsTracked);

    private sealed record ListTrackedPlayersEnvelope(
        [property: JsonPropertyName("items")] List<ListTrackedPlayerItem> Items);

    private sealed record ListTrackedPlayerItem(
        [property: JsonPropertyName("trackedPlayerId")] Guid TrackedPlayerId,
        [property: JsonPropertyName("isActive")] bool IsActive);
}
