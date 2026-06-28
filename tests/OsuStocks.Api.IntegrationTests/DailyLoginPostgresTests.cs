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

/// <summary>
/// Daily-login coverage against a real PostgreSQL database, exercising the actual migration, the
/// uq_daily_login_rewards_user_date unique index (idempotency), wallet RowVersion concurrency, and
/// single-transaction atomicity — none of which the in-memory harness can reproduce.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DailyLoginPostgresTests(PostgresTestcontainerFixture fixture)
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Claim_FirstTime_GrantsDayOneAndPersistsLedgerAndTransaction()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();
        await SeedUserAndWalletAsync(factory, 70_001, balance: 1000m);

        var payload = await ClaimAsync(client);

        Assert.True(payload.Granted);
        Assert.False(payload.AlreadyClaimed);
        Assert.Equal(1, payload.StreakDay);
        Assert.Equal(1500m, payload.Amount);
        Assert.Equal(2500m, payload.NewBalance);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rewards = await dbContext.DailyLoginRewards.Where(x => x.UserId == TestUserId).ToListAsync();
        var reward = Assert.Single(rewards);
        Assert.Equal(1, reward.StreakDay);
        Assert.Equal(1500m, reward.Amount);
        Assert.Equal(DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime), reward.RewardDate);

        var wallet = await dbContext.Wallets.AsNoTracking().FirstAsync(x => x.UserId == TestUserId);
        Assert.Equal(2500m, wallet.Balance);

        var txns = await dbContext.WalletTransactions
            .AsNoTracking()
            .Where(x => x.WalletId == wallet.Id && x.TransactionType == WalletTransactionType.DailyReward)
            .ToListAsync();
        var txn = Assert.Single(txns);
        Assert.Equal(1500m, txn.Amount);
        Assert.Equal(reward.Id, txn.ReferenceId);

        // The denormalized cache on the user matches the ledger.
        var user = await dbContext.Users.AsNoTracking().FirstAsync(x => x.Id == TestUserId);
        Assert.Equal(1, user.DailyRewardStreak);
        Assert.Equal(reward.RewardDate, user.LastDailyRewardDate);
    }

    [Fact]
    public async Task Claim_Twice_IsIdempotentAndCreditsOnce()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();
        await SeedUserAndWalletAsync(factory, 70_002, balance: 1000m);

        var first = await ClaimAsync(client);
        Assert.True(first.Granted);

        var second = await ClaimAsync(client);
        Assert.False(second.Granted);
        Assert.True(second.AlreadyClaimed);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rewardCount = await dbContext.DailyLoginRewards.CountAsync(x => x.UserId == TestUserId);
        Assert.Equal(1, rewardCount);

        var wallet = await dbContext.Wallets.AsNoTracking().FirstAsync(x => x.UserId == TestUserId);
        Assert.Equal(2500m, wallet.Balance);
    }

    [Fact]
    public async Task Claim_OnConsecutiveDay_AdvancesStreak()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();
        await SeedUserAndWalletAsync(factory, 70_003, balance: 1000m);
        await SeedLedgerAsync(factory, DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime).AddDays(-1), streakDay: 3, amount: 10000m);

        var payload = await ClaimAsync(client);

        Assert.True(payload.Granted);
        Assert.Equal(4, payload.StreakDay);
        Assert.Equal(4500m, payload.Amount);
    }

    [Fact]
    public async Task Claim_AfterFinalDay_WrapsToDayOne()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();
        await SeedUserAndWalletAsync(factory, 70_004, balance: 1000m);
        await SeedLedgerAsync(factory, DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime).AddDays(-1), streakDay: 7, amount: 30000m);

        var payload = await ClaimAsync(client);

        Assert.True(payload.Granted);
        Assert.Equal(1, payload.StreakDay);
        Assert.Equal(1500m, payload.Amount);
    }

    [Fact]
    public async Task Claim_AfterMissedDay_ResetsToDayOne()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();
        await SeedUserAndWalletAsync(factory, 70_005, balance: 1000m);
        await SeedLedgerAsync(factory, DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime).AddDays(-3), streakDay: 5, amount: 15000m);

        var payload = await ClaimAsync(client);

        Assert.True(payload.Granted);
        Assert.Equal(1, payload.StreakDay);
        Assert.Equal(1500m, payload.Amount);
    }

    [Fact]
    public async Task GetStatus_IsReadOnly()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();
        await SeedUserAndWalletAsync(factory, 70_006, balance: 1000m);

        var response = await client.GetAsync("/api/v1/daily-login");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.NotNull(payload);
        Assert.False(payload!.ClaimedToday);
        Assert.Equal(1500m, payload.TodayAmount);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await dbContext.DailyLoginRewards.CountAsync(x => x.UserId == TestUserId));
        var wallet = await dbContext.Wallets.AsNoTracking().FirstAsync(x => x.UserId == TestUserId);
        Assert.Equal(1000m, wallet.Balance);
    }

    [Fact]
    public async Task Claim_ConcurrentSameDay_GrantsExactlyOnce()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();
        await SeedUserAndWalletAsync(factory, 70_007, balance: 1000m);

        // Fire several simultaneous claims; the unique (user, date) index must let exactly one through.
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => client.PostAsync("/api/v1/daily-login/claim", content: null))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        // Every response is either a 200 (granted or already-claimed) or a 409 retryable concurrency
        // conflict — never a 500.
        Assert.All(responses, r => Assert.True(
            r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"Unexpected status {r.StatusCode}"));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The authoritative invariant: exactly one ledger row and the wallet credited exactly once.
        Assert.Equal(1, await dbContext.DailyLoginRewards.CountAsync(x => x.UserId == TestUserId));
        var wallet = await dbContext.Wallets.AsNoTracking().FirstAsync(x => x.UserId == TestUserId);
        Assert.Equal(2500m, wallet.Balance);
    }

    private static async Task<ClaimResponse> ClaimAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/v1/daily-login/claim", content: null);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ClaimResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private static async Task SeedUserAndWalletAsync(PostgresWebApplicationFactory factory, long osuUserId, decimal balance)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Users.Add(new User
        {
            Id = TestUserId,
            OsuUserId = osuUserId,
            Username = $"user-{osuUserId}",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        });

        dbContext.Wallets.Add(new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            Balance = balance,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedLedgerAsync(
        PostgresWebApplicationFactory factory,
        DateOnly rewardDate,
        int streakDay,
        decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.DailyLoginRewards.Add(new DailyLoginReward
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            RewardDate = rewardDate,
            StreakDay = streakDay,
            Amount = amount,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed record StatusResponse(
        [property: JsonPropertyName("streak")] int Streak,
        [property: JsonPropertyName("claimedToday")] bool ClaimedToday,
        [property: JsonPropertyName("todayAmount")] decimal TodayAmount,
        [property: JsonPropertyName("schedule")] List<decimal> Schedule,
        [property: JsonPropertyName("serverTimeUtc")] DateTimeOffset ServerTimeUtc,
        [property: JsonPropertyName("nextResetUtc")] DateTimeOffset NextResetUtc);

    private sealed record ClaimResponse(
        [property: JsonPropertyName("granted")] bool Granted,
        [property: JsonPropertyName("alreadyClaimed")] bool AlreadyClaimed,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("streakDay")] int StreakDay,
        [property: JsonPropertyName("newBalance")] decimal NewBalance);
}
