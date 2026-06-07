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
public sealed class LeaderboardEndpointsTests(PostgresTestcontainerFixture fixture)
{
    // Known share price for the shared stock. Holdings value = quantity * CurrentPrice.
    private const decimal CurrentPrice = 10m;

    // Three seeded users with deterministic wealth/profit/volume so ranks are unambiguous.
    // These are distinct from the authed caller (11111111-...), which is NOT seeded as a User row
    // and therefore never appears in the leaderboards (the queries enumerate dbContext.Users).
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000b2");
    private static readonly Guid UserC = Guid.Parse("cccccccc-0000-0000-0000-0000000000c3");

    [Fact]
    public async Task GetWealthLeaderboard_Daily_OrdersDescendingByWealth_WithSequentialRanks()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        await SeedAsync(factory);

        var response = await client.GetAsync("/api/v1/leaderboards/wealth?period=daily");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LeaderboardEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal("daily", payload!.Period);
        Assert.Equal(3, payload.Items.Count);

        // Wealth: A = 1000 + 100*10 = 2000 ; B = 500 + 50*10 = 1000 ; C = 100 + 0 = 100.
        AssertEntry(payload.Items[0], rank: 1, userId: UserA, value: 2000m);
        AssertEntry(payload.Items[1], rank: 2, userId: UserB, value: 1000m);
        AssertEntry(payload.Items[2], rank: 3, userId: UserC, value: 100m);
    }

    [Fact]
    public async Task GetProfitLeaderboard_Daily_OrdersDescendingByProfit_IncludingNegative()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        await SeedAsync(factory);

        var response = await client.GetAsync("/api/v1/leaderboards/profit?period=daily");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LeaderboardEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal("daily", payload!.Period);
        Assert.Equal(3, payload.Items.Count);

        // Profit = Wealth - NetDeposits. Each user has InitialGrant 1000 -> NetDeposits = 1000.
        // A = 2000 - 1000 = 1000 ; B = 1000 - 1000 = 0 ; C = 100 - 1000 = -900.
        AssertEntry(payload.Items[0], rank: 1, userId: UserA, value: 1000m);
        AssertEntry(payload.Items[1], rank: 2, userId: UserB, value: 0m);
        AssertEntry(payload.Items[2], rank: 3, userId: UserC, value: -900m);
    }

    [Fact]
    public async Task GetTraderLeaderboard_Daily_OrdersDescendingByTradedVolume()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        await SeedAsync(factory);

        var response = await client.GetAsync("/api/v1/leaderboards/traders?period=daily");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LeaderboardEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal("daily", payload!.Period);
        Assert.Equal(3, payload.Items.Count);

        // Trades within the last 24h: A total_amount sum = 5000 ; B = 3000 ; C = 1000.
        AssertEntry(payload.Items[0], rank: 1, userId: UserA, value: 5000m);
        AssertEntry(payload.Items[1], rank: 2, userId: UserB, value: 3000m);
        AssertEntry(payload.Items[2], rank: 3, userId: UserC, value: 1000m);
    }

    [Fact]
    public async Task GetWealthLeaderboard_WithPaging_ReturnsCorrectSliceAndRanksAcrossPages()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        await SeedAsync(factory);

        // Page 1, pageSize 2 -> top two by wealth (A, B) with ranks 1 and 2.
        var firstPage = await client.GetAsync("/api/v1/leaderboards/wealth?period=daily&page=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);

        var firstPayload = await firstPage.Content.ReadFromJsonAsync<LeaderboardEnvelope>();
        Assert.NotNull(firstPayload);
        Assert.Equal(1, firstPayload!.Page);
        Assert.Equal(2, firstPayload.PageSize);
        Assert.Equal(2, firstPayload.Items.Count);
        AssertEntry(firstPayload.Items[0], rank: 1, userId: UserA, value: 2000m);
        AssertEntry(firstPayload.Items[1], rank: 2, userId: UserB, value: 1000m);

        // Page 2, pageSize 2 -> the remaining third user (C) with rank 3.
        var secondPage = await client.GetAsync("/api/v1/leaderboards/wealth?period=daily&page=2&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);

        var secondPayload = await secondPage.Content.ReadFromJsonAsync<LeaderboardEnvelope>();
        Assert.NotNull(secondPayload);
        Assert.Equal(2, secondPayload!.Page);
        Assert.Equal(2, secondPayload.PageSize);
        var only = Assert.Single(secondPayload.Items);
        AssertEntry(only, rank: 3, userId: UserC, value: 100m);
    }

    [Fact]
    public async Task GetWealthLeaderboard_WithInvalidPeriod_ReturnsBadRequest()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/leaderboards/wealth?period=yearly");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("VALIDATION_ERROR", error!.Code);
    }

    private static void AssertEntry(LeaderboardEntry entry, int rank, Guid userId, decimal value)
    {
        Assert.Equal(rank, entry.Rank);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(value, entry.Value);
        // Username is populated from the seeded user row.
        Assert.False(string.IsNullOrWhiteSpace(entry.Username));
    }

    private static async Task SeedAsync(PostgresWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;
        var withinWindow = now.AddHours(-1);

        // Shared stock all holdings/trades reference, with a known current price.
        var trackedPlayer = new TrackedPlayer
        {
            Id = Guid.NewGuid(),
            OsuUserId = 920100,
            Username = "leaderboard-player",
            TrackingTier = TrackingTier.Tier1,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "seed"
        };
        dbContext.TrackedPlayers.Add(trackedPlayer);

        var stock = new PlayerStock
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = trackedPlayer.Id,
            CurrentPrice = CurrentPrice,
            DemandScore = 1m,
            PerformanceScore = 1m,
            CreatedAt = now,
            LastUpdated = now,
            CreatedBy = "seed"
        };
        dbContext.PlayerStocks.Add(stock);

        // User A: wallet 1000, holding 100 (value 1000) -> wealth 2000 ; deposit 1000 -> profit 1000.
        SeedUser(dbContext, UserA, osuUserId: 920001, username: "user-a-leaderboard",
            walletBalance: 1000m, holdingQuantity: 100, stock.Id, now);
        // User B: wallet 500, holding 50 (value 500) -> wealth 1000 ; deposit 1000 -> profit 0.
        SeedUser(dbContext, UserB, osuUserId: 920002, username: "user-b-leaderboard",
            walletBalance: 500m, holdingQuantity: 50, stock.Id, now);
        // User C: wallet 100, no holdings -> wealth 100 ; deposit 1000 -> profit -900.
        SeedUser(dbContext, UserC, osuUserId: 920003, username: "user-c-leaderboard",
            walletBalance: 100m, holdingQuantity: 0, stock.Id, now);

        // Trades within the last 24h drive the trader leaderboard volume (SUM of total_amount).
        // A = 5000 (3000 + 2000), B = 3000, C = 1000.
        dbContext.Trades.Add(CreateTrade(UserA, stock.Id, totalAmount: 3000m, withinWindow));
        dbContext.Trades.Add(CreateTrade(UserA, stock.Id, totalAmount: 2000m, withinWindow));
        dbContext.Trades.Add(CreateTrade(UserB, stock.Id, totalAmount: 3000m, withinWindow));
        dbContext.Trades.Add(CreateTrade(UserC, stock.Id, totalAmount: 1000m, withinWindow));

        await dbContext.SaveChangesAsync();
    }

    private static void SeedUser(
        AppDbContext dbContext,
        Guid userId,
        long osuUserId,
        string username,
        decimal walletBalance,
        int holdingQuantity,
        Guid stockId,
        DateTimeOffset now)
    {
        dbContext.Users.Add(new User
        {
            Id = userId,
            OsuUserId = osuUserId,
            Username = username,
            Role = UserRole.User,
            CreatedAt = now,
            CreatedBy = "seed"
        });

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Balance = walletBalance,
            CreatedAt = now,
            CreatedBy = "seed"
        };
        dbContext.Wallets.Add(wallet);

        // InitialGrant 1000 -> NetDeposits = 1000 for every user (profit baseline).
        dbContext.WalletTransactions.Add(new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionType = WalletTransactionType.InitialGrant,
            Amount = 1000m,
            CreatedAt = now
        });

        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now,
            CreatedBy = "seed"
        };
        dbContext.Portfolios.Add(portfolio);

        if (holdingQuantity > 0)
        {
            dbContext.Holdings.Add(new Holding
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolio.Id,
                StockId = stockId,
                Quantity = holdingQuantity,
                AveragePrice = CurrentPrice,
                CreatedAt = now,
                CreatedBy = "seed"
            });
        }
    }

    private static Trade CreateTrade(Guid userId, Guid stockId, decimal totalAmount, DateTimeOffset executedAt)
    {
        // Quantity/unitPrice are not asserted by the trader leaderboard (it sums total_amount);
        // keep them internally consistent for realism.
        const int quantity = 1;
        return new Trade
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StockId = stockId,
            TradeType = TradeType.Buy,
            Quantity = quantity,
            UnitPrice = totalAmount,
            TotalAmount = totalAmount,
            ExecutedAt = executedAt
        };
    }

    private sealed record LeaderboardEnvelope(
        [property: JsonPropertyName("items")] List<LeaderboardEntry> Items,
        [property: JsonPropertyName("period")] string Period,
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("pageSize")] int PageSize);

    private sealed record LeaderboardEntry(
        [property: JsonPropertyName("rank")] int Rank,
        [property: JsonPropertyName("userId")] Guid UserId,
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("avatarUrl")] string? AvatarUrl,
        [property: JsonPropertyName("value")] decimal Value,
        [property: JsonPropertyName("periodChange")] decimal? PeriodChange);

    private sealed record ErrorResponse(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message);
}
