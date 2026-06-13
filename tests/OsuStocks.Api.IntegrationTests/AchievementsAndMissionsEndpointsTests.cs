using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using OsuStocks.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

/// <summary>
/// Exercises achievements and missions end-to-end against the real Postgres-backed stack: the
/// read endpoints, derived progress, post-commit unlock/completion on trades, idempotency, and the
/// credit rewards (wallet + ledger). The authenticated caller is always <see cref="TestUserId"/>.
/// Trades use distinct stocks (each a first-buyer) so neither the per-stock cooldown nor the
/// ownership limit interferes with multi-trade missions.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AchievementsAndMissionsEndpointsTests(PostgresTestcontainerFixture fixture)
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetAchievements_FreshUser_ReturnsCatalogAllLocked()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        await SeedAsync(factory, stockCount: 0);

        var response = await client.GetAsync("/api/v1/achievements");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AchievementsDto>();
        Assert.NotNull(body);
        Assert.Equal(0, body.UnlockedCount);
        Assert.True(body.TotalCount > 0);
        Assert.Equal(body.TotalCount, body.Items.Count);
        Assert.All(body.Items, i =>
        {
            Assert.False(i.Unlocked);
            Assert.Null(i.UnlockedAt);
            Assert.True(i.CurrentValue >= 0, $"{i.Code} progress should be non-negative.");
        });
        // Trade-derived achievements start at 0 for a user who has never traded.
        Assert.Equal(0, Assert.Single(body.Items, i => i.Code == "first-trade").CurrentValue);
        // The investor level defaults to 1, so level achievements legitimately show progress 1.
        Assert.Equal(1, Assert.Single(body.Items, i => i.Code == "level-10").CurrentValue);
    }

    [Fact]
    public async Task GetMissions_FreshUser_ReturnsDailyAndWeeklyAtZeroProgress()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        await SeedAsync(factory, stockCount: 0);

        var response = await client.GetAsync("/api/v1/missions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MissionsDto>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.Items);
        Assert.Contains(body.Items, m => m.Period == "Daily");
        Assert.Contains(body.Items, m => m.Period == "Weekly");
        Assert.All(body.Items, m =>
        {
            Assert.False(m.Completed);
            Assert.Null(m.CompletedAt);
            Assert.Equal(0, m.CurrentValue);
            Assert.False(string.IsNullOrWhiteSpace(m.PeriodKey));
            Assert.True(m.ResetsAt > DateTimeOffset.UtcNow);
        });
    }

    [Fact]
    public async Task Buying_UnlocksAchievements_GrantsCredits_Notifies_AndIsIdempotent()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        // Two stocks at price 100. A big first buy on stock 0 = 100,000 volume => first-trade + volume-100k.
        var stocks = await SeedAsync(factory, stockCount: 2, stockPrice: 100m, walletBalance: 2_000_000m);

        (await client.PostAsJsonAsync("/api/v1/trading/buy", new TradeRequest(stocks[0], 1000))).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var unlocked = await db.UserAchievements.AsNoTracking()
                .Where(x => x.UserId == TestUserId).Select(x => x.AchievementCode).ToListAsync();
            Assert.Contains("first-trade", unlocked);
            Assert.Contains("volume-100k", unlocked);

            var rewardLedger = await db.WalletTransactions.AsNoTracking()
                .Where(t => t.Wallet.UserId == TestUserId
                    && t.TransactionType == WalletTransactionType.AchievementReward)
                .ToListAsync();
            Assert.Contains(rewardLedger, t => t.Amount == 1_000m);  // first-trade
            Assert.Contains(rewardLedger, t => t.Amount == 5_000m);  // volume-100k

            var notifications = await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == TestUserId && n.Type == "AchievementUnlocked")
                .ToListAsync();
            Assert.True(notifications.Count >= 2);

            // The notification data payload is an FE contract: { code, name, rewardCredits }.
            var firstTrade = Assert.Single(notifications, n =>
                n.Data is not null && n.Data.Contains("\"first-trade\""));
            using var payload = JsonDocument.Parse(firstTrade.Data!);
            Assert.Equal("first-trade", payload.RootElement.GetProperty("code").GetString());
            Assert.Equal("First Steps", payload.RootElement.GetProperty("name").GetString());
            Assert.Equal(1000, payload.RootElement.GetProperty("rewardCredits").GetInt64());
        }

        // A second trade on a different stock must not re-unlock first-trade (idempotent).
        (await client.PostAsJsonAsync("/api/v1/trading/buy", new TradeRequest(stocks[1], 1))).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var firstTradeRows = await db.UserAchievements.AsNoTracking()
                .CountAsync(x => x.UserId == TestUserId && x.AchievementCode == "first-trade");
            Assert.Equal(1, firstTradeRows);
        }
    }

    [Fact]
    public async Task DailyMission_CompletesAtThreeTrades_GrantsCredit_Notifies_AndIsIdempotent()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        // Four stocks at a low price so volume-based missions/achievements stay untouched.
        var stocks = await SeedAsync(factory, stockCount: 4, stockPrice: 100m, walletBalance: 2_000_000m);

        // Three trades across distinct stocks => daily-trade-3 (target 3) completes on the third.
        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync("/api/v1/trading/buy", new TradeRequest(stocks[i], 1))).EnsureSuccessStatusCode();
        }

        long initialMissionRewardCount;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var completions = await db.UserMissionCompletions.AsNoTracking()
                .Where(x => x.UserId == TestUserId && x.MissionCode == "daily-trade-3").ToListAsync();
            var completion = Assert.Single(completions);
            Assert.Equal(3_000L, completion.RewardCredits);

            var reward = await db.WalletTransactions.AsNoTracking()
                .Where(t => t.Wallet.UserId == TestUserId && t.TransactionType == WalletTransactionType.MissionReward)
                .ToListAsync();
            Assert.Contains(reward, t => t.Amount == 3_000m);
            initialMissionRewardCount = reward.Count;

            var notified = await db.Notifications.AsNoTracking()
                .CountAsync(n => n.UserId == TestUserId && n.Type == "MissionCompleted");
            Assert.True(notified >= 1);
        }

        // A fourth trade (on the remaining stock) must not re-complete or re-reward daily-trade-3.
        (await client.PostAsJsonAsync("/api/v1/trading/buy", new TradeRequest(stocks[3], 1))).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var completions = await db.UserMissionCompletions.AsNoTracking()
                .CountAsync(x => x.UserId == TestUserId && x.MissionCode == "daily-trade-3");
            Assert.Equal(1, completions);

            var missionRewardCount = await db.WalletTransactions.AsNoTracking()
                .CountAsync(t => t.Wallet.UserId == TestUserId
                    && t.TransactionType == WalletTransactionType.MissionReward);
            Assert.Equal(initialMissionRewardCount, missionRewardCount);
        }

        // The endpoint reflects completion for the current daily period.
        var missions = await (await client.GetAsync("/api/v1/missions")).Content.ReadFromJsonAsync<MissionsDto>();
        Assert.NotNull(missions);
        var daily = Assert.Single(missions.Items, m => m.Code == "daily-trade-3");
        Assert.True(daily.Completed);
        Assert.Equal(3, daily.CurrentValue);
    }

    [Fact]
    public async Task RewardCredits_AreNeutralOnProfitLeaderboard()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);

        // Balance 103,000 = InitialGrant 100,000 + MissionReward 3,000; no holdings.
        // Wealth = 103,000; NetDeposits must include the reward (100,000 + 3,000) => Profit = 0.
        // If MissionReward were NOT treated as a deposit, profit would read 3,000.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                Id = TestUserId,
                OsuUserId = 999999,
                Username = "integration-progression",
                Role = UserRole.User,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            });
            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                UserId = TestUserId,
                Balance = 103_000m,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };
            db.Wallets.Add(wallet);
            db.WalletTransactions.Add(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                TransactionType = WalletTransactionType.InitialGrant,
                Amount = 100_000m,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.WalletTransactions.Add(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                TransactionType = WalletTransactionType.MissionReward,
                Amount = 3_000m,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var leaderboard = scope.ServiceProvider.GetRequiredService<ILeaderboardReadRepository>();
            var rows = await leaderboard.GetProfitAsync(DateTimeOffset.UtcNow.AddDays(-1), 0, 50);

            var entry = Assert.Single(rows, r => r.UserId == TestUserId);
            Assert.Equal(0m, entry.Value);
        }
    }

    private async Task<IReadOnlyList<Guid>> SeedAsync(
        PostgresWebApplicationFactory factory,
        int stockCount,
        decimal stockPrice = 100m,
        decimal walletBalance = 1_000_000m)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Users.Add(new User
        {
            Id = TestUserId,
            OsuUserId = 999999,
            Username = "integration-progression",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        });

        db.Wallets.Add(new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            Balance = walletBalance,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        });

        db.Portfolios.Add(new Portfolio
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        });

        var stockIds = new List<Guid>();
        for (var i = 0; i < stockCount; i++)
        {
            var player = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = 700_000 + i,
                Username = $"player-{i}",
                TrackingTier = TrackingTier.Tier2,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };
            db.TrackedPlayers.Add(player);

            var stock = new PlayerStock
            {
                Id = Guid.NewGuid(),
                TrackedPlayerId = player.Id,
                CurrentPrice = stockPrice,
                DemandScore = 1m,
                PerformanceScore = 1m,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };
            db.PlayerStocks.Add(stock);
            stockIds.Add(stock.Id);
        }

        await db.SaveChangesAsync();
        return stockIds;
    }

    private sealed record TradeRequest(Guid StockId, int Quantity);

    private sealed record AchievementsDto(
        [property: JsonPropertyName("unlockedCount")] int UnlockedCount,
        [property: JsonPropertyName("totalCount")] int TotalCount,
        [property: JsonPropertyName("items")] List<AchievementItemDto> Items);

    private sealed record AchievementItemDto(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("currentValue")] long CurrentValue,
        [property: JsonPropertyName("unlocked")] bool Unlocked,
        [property: JsonPropertyName("unlockedAt")] DateTimeOffset? UnlockedAt);

    private sealed record MissionsDto(
        [property: JsonPropertyName("items")] List<MissionItemDto> Items);

    private sealed record MissionItemDto(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("period")] string Period,
        [property: JsonPropertyName("periodKey")] string PeriodKey,
        [property: JsonPropertyName("currentValue")] long CurrentValue,
        [property: JsonPropertyName("completed")] bool Completed,
        [property: JsonPropertyName("completedAt")] DateTimeOffset? CompletedAt,
        [property: JsonPropertyName("resetsAt")] DateTimeOffset ResetsAt);
}
