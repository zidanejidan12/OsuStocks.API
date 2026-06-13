using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

/// <summary>
/// Exercises investor levels end-to-end against the real Postgres-backed stack: the
/// <c>GET /investor/level</c> read endpoint, XP awarded from trading volume on buy/sell, the
/// <c>investorLevel</c> block on <c>/me</c>, and the level-up notification. The authenticated
/// caller is always <see cref="TestUserId"/> (see <see cref="TestAuthHandler"/>).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvestorLevelEndpointsTests(PostgresTestcontainerFixture fixture)
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetInvestorLevel_FreshUser_ReturnsLevel1WithZeroXp()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Users.Add(CreateUser());
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/investor/level");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var level = await response.Content.ReadFromJsonAsync<InvestorLevelDto>();
        Assert.NotNull(level);
        Assert.Equal(1, level.Level);
        Assert.Equal(0L, level.TotalXp);
        Assert.Equal("Novice Investor", level.Title);
        Assert.Equal(0L, level.XpIntoLevel);
        Assert.True(level.XpForNextLevel > 0L);
        Assert.Equal(0d, level.ProgressToNext);
    }

    [Fact]
    public async Task Buying_AwardsXpEqualToTradeVolume_AndIsExposedOnEndpointAndMe()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        // Price 100, buy 50 shares => 5,000 traded credits => 5,000 XP. Stays level 1.
        var stockId = await SeedTradeableStockAsync(factory, stockPrice: 100m, walletBalance: 1_000_000m);

        var buy = await client.PostAsJsonAsync("/api/v1/trading/buy", new TradeRequest(stockId, 50));
        buy.EnsureSuccessStatusCode();

        const long expectedXp = 5_000L;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var profile = await dbContext.InvestorProfiles.AsNoTracking()
                .SingleAsync(x => x.UserId == TestUserId);

            Assert.Equal(expectedXp, profile.TotalXp);
            Assert.Equal(1, profile.Level);
        }

        var levelResponse = await client.GetAsync("/api/v1/investor/level");
        levelResponse.EnsureSuccessStatusCode();
        var level = await levelResponse.Content.ReadFromJsonAsync<InvestorLevelDto>();
        Assert.NotNull(level);
        Assert.Equal(expectedXp, level.TotalXp);
        Assert.Equal(1, level.Level);
        Assert.Equal(expectedXp, level.XpIntoLevel);

        // /me embeds the same standing.
        var meResponse = await client.GetAsync("/api/v1/auth/me");
        meResponse.EnsureSuccessStatusCode();
        var me = await meResponse.Content.ReadFromJsonAsync<MeDto>();
        Assert.NotNull(me);
        Assert.NotNull(me.InvestorLevel);
        Assert.Equal(expectedXp, me.InvestorLevel!.TotalXp);
        Assert.Equal(1, me.InvestorLevel.Level);

        // Selling also awards XP; XP accrues additively across trades and the profile is reused
        // (exactly one row), not overwritten or duplicated.
        var sell = await client.PostAsJsonAsync("/api/v1/trading/sell", new TradeRequest(stockId, 50));
        sell.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var profiles = await dbContext.InvestorProfiles.AsNoTracking()
                .Where(x => x.UserId == TestUserId)
                .ToListAsync();
            var profile = Assert.Single(profiles);

            // Expected XP = sum of floor(traded volume) over every executed trade. The sell's unit
            // price may differ from the buy's because trading reprices the stock, so derive the
            // expected total from the actual trade rows rather than hardcoding it.
            var trades = await dbContext.Trades.AsNoTracking()
                .Where(x => x.UserId == TestUserId)
                .ToListAsync();
            var expectedTotal = trades.Sum(t => (long)decimal.Floor(t.TotalAmount));

            Assert.Equal(2, trades.Count);
            Assert.Equal(expectedTotal, profile.TotalXp);
            Assert.True(profile.TotalXp > expectedXp,
                $"Selling should award additional XP; total was {profile.TotalXp}.");
        }
    }

    [Fact]
    public async Task Buying_AtFractionalPrice_FloorsTradedVolumeIntoXp()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        // Price 1.5, buy 3 shares => 4.5 traded credits => floor => 4 XP (not 5, not 4.5).
        var stockId = await SeedTradeableStockAsync(factory, stockPrice: 1.5m, walletBalance: 1_000m);

        var buy = await client.PostAsJsonAsync("/api/v1/trading/buy", new TradeRequest(stockId, 3));
        buy.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await dbContext.InvestorProfiles.AsNoTracking()
            .SingleAsync(x => x.UserId == TestUserId);

        Assert.Equal(4L, profile.TotalXp);
    }

    [Fact]
    public async Task Buying_CrossingLevelBoundary_AdvancesLevel_AndCreatesLevelUpNotification()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        // Price 100, buy 300 shares => 30,000 traded credits => 30,000 XP == the level-2 floor.
        var stockId = await SeedTradeableStockAsync(factory, stockPrice: 100m, walletBalance: 1_000_000m);

        var buy = await client.PostAsJsonAsync("/api/v1/trading/buy", new TradeRequest(stockId, 300));
        buy.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var profile = await dbContext.InvestorProfiles.AsNoTracking()
                .SingleAsync(x => x.UserId == TestUserId);
            Assert.Equal(30_000L, profile.TotalXp);
            Assert.Equal(2, profile.Level);
            Assert.NotNull(profile.LastLevelUpAt);

            var levelUp = await dbContext.Notifications.AsNoTracking()
                .Where(x => x.UserId == TestUserId && x.Type == "InvestorLevelUp")
                .ToListAsync();
            var notification = Assert.Single(levelUp);
            Assert.Contains("level 2", notification.Title);
            Assert.False(notification.IsRead);

            // The data payload is a documented FE contract: {"level":<int>,"title":"<string>"}.
            Assert.NotNull(notification.Data);
            using var payload = JsonDocument.Parse(notification.Data!);
            Assert.Equal(2, payload.RootElement.GetProperty("level").GetInt32());
            Assert.Equal("Novice Investor", payload.RootElement.GetProperty("title").GetString());
        }

        var levelResponse = await client.GetAsync("/api/v1/investor/level");
        levelResponse.EnsureSuccessStatusCode();
        var level = await levelResponse.Content.ReadFromJsonAsync<InvestorLevelDto>();
        Assert.NotNull(level);
        Assert.Equal(2, level.Level);
        // Level 2 is still within the 1-9 band.
        Assert.Equal("Novice Investor", level.Title);
    }

    private async Task<Guid> SeedTradeableStockAsync(
        PostgresWebApplicationFactory factory,
        decimal stockPrice,
        decimal walletBalance)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Users.Add(CreateUser());

        dbContext.Wallets.Add(new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            Balance = walletBalance,
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
            OsuUserId = 808080,
            Username = "player-investor",
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

        await dbContext.SaveChangesAsync();
        return stock.Id;
    }

    private static User CreateUser()
    {
        return new User
        {
            Id = TestUserId,
            OsuUserId = 999999,
            Username = "integration-investor",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        };
    }

    private sealed record TradeRequest(Guid StockId, int Quantity);

    private sealed record InvestorLevelDto(
        [property: JsonPropertyName("level")] int Level,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("totalXp")] long TotalXp,
        [property: JsonPropertyName("xpIntoLevel")] long XpIntoLevel,
        [property: JsonPropertyName("xpForNextLevel")] long XpForNextLevel,
        [property: JsonPropertyName("progressToNext")] double ProgressToNext);

    private sealed record MeDto(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("investorLevel")] InvestorLevelDto? InvestorLevel);
}
