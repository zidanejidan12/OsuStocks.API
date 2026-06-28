using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

/// <summary>
/// Functional daily-login coverage on the in-memory harness (no database required). The authenticated user
/// is always <see cref="TestAuthHandler"/>'s fixed id, so tests seed a matching user + wallet.
/// </summary>
public sealed class DailyLoginEndpointsTests
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static DateOnly Today => DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

    [Fact]
    public async Task GetStatus_NoPriorClaims_ReturnsDayOneUnclaimed()
    {
        await using var factory = new CustomWebApplicationFactory();
        await SeedUserAndWalletAsync(factory, balance: 1000m);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/daily-login");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.Streak);
        Assert.False(payload.ClaimedToday);
        Assert.Equal(1500m, payload.TodayAmount);
        Assert.Equal(7, payload.Schedule.Count);
        Assert.Equal(1500m, payload.Schedule[0]);
        Assert.Equal(10000m, payload.Schedule[6]);
        Assert.True(payload.NextResetUtc > payload.ServerTimeUtc);
    }

    [Fact]
    public async Task GetStatus_DoesNotGrant()
    {
        await using var factory = new CustomWebApplicationFactory();
        await SeedUserAndWalletAsync(factory, balance: 1000m);
        using var client = factory.CreateClient();

        await client.GetAsync("/api/v1/daily-login");

        using var scope = factory.Services.CreateScope();
        var wallets = scope.ServiceProvider.GetRequiredService<InMemoryWalletRepository>();
        var ledger = scope.ServiceProvider.GetRequiredService<InMemoryDailyLoginRewardRepository>();
        var wallet = await wallets.GetByUserIdAsync(TestUserId);

        Assert.Equal(1000m, wallet!.Balance);
        Assert.Equal(0, ledger.Count);
    }

    [Fact]
    public async Task Claim_FirstTimeToday_GrantsDayOne()
    {
        await using var factory = new CustomWebApplicationFactory();
        await SeedUserAndWalletAsync(factory, balance: 1000m);
        using var client = factory.CreateClient();

        var payload = await ClaimAsync(client);

        Assert.True(payload.Granted);
        Assert.False(payload.AlreadyClaimed);
        Assert.Equal(1500m, payload.Amount);
        Assert.Equal(1, payload.StreakDay);
        Assert.Equal(2500m, payload.NewBalance);

        // The ledger row is the authoritative idempotency record — verify it was actually persisted.
        using var scope = factory.Services.CreateScope();
        var ledger = scope.ServiceProvider.GetRequiredService<InMemoryDailyLoginRewardRepository>();
        var reward = await ledger.GetByUserAndDateAsync(TestUserId, Today, CancellationToken.None);
        Assert.NotNull(reward);
        Assert.Equal(1, reward!.StreakDay);
        Assert.Equal(1500m, reward.Amount);
        Assert.Equal(1, ledger.Count);
    }

    [Fact]
    public async Task Claim_Twice_SecondIsIdempotentAndCreditsOnce()
    {
        await using var factory = new CustomWebApplicationFactory();
        await SeedUserAndWalletAsync(factory, balance: 1000m);
        using var client = factory.CreateClient();

        var first = await ClaimAsync(client);
        Assert.True(first.Granted);

        var second = await ClaimAsync(client);
        Assert.False(second.Granted);
        Assert.True(second.AlreadyClaimed);
        Assert.Equal(1500m, second.Amount);
        Assert.Equal(1, second.StreakDay);

        using var scope = factory.Services.CreateScope();
        var wallets = scope.ServiceProvider.GetRequiredService<InMemoryWalletRepository>();
        var wallet = await wallets.GetByUserIdAsync(TestUserId);
        Assert.Equal(2500m, wallet!.Balance);

        // Exactly one ledger row despite two claims.
        var ledger = scope.ServiceProvider.GetRequiredService<InMemoryDailyLoginRewardRepository>();
        Assert.Equal(1, ledger.Count);
    }

    [Fact]
    public async Task GetStatus_AfterClaim_ReflectsClaimedState()
    {
        await using var factory = new CustomWebApplicationFactory();
        await SeedUserAndWalletAsync(factory, balance: 1000m);
        using var client = factory.CreateClient();

        await ClaimAsync(client);

        var response = await client.GetAsync("/api/v1/daily-login");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<StatusResponse>();

        Assert.NotNull(payload);
        Assert.True(payload.ClaimedToday);
        Assert.Equal(1, payload.Streak);
        Assert.Equal(1500m, payload.TodayAmount);
    }

    [Fact]
    public async Task GetStatus_UnclaimedConsecutiveDay_PreviewsNextDayConsistently()
    {
        await using var factory = new CustomWebApplicationFactory();
        await SeedUserAndWalletAsync(factory, balance: 1000m);
        await SeedLedgerAsync(factory, rewardDate: Today.AddDays(-1), streakDay: 3, amount: 10000m);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/daily-login");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<StatusResponse>();

        Assert.NotNull(payload);
        Assert.False(payload.ClaimedToday);
        // streak and todayAmount must describe the SAME day: claiming now grants day 4.
        Assert.Equal(4, payload.Streak);
        Assert.Equal(4500m, payload.TodayAmount);
    }

    [Fact]
    public async Task GetStatus_UnclaimedDayAfterFinalDay_WrapsToDayOneConsistently()
    {
        await using var factory = new CustomWebApplicationFactory();
        await SeedUserAndWalletAsync(factory, balance: 1000m);
        await SeedLedgerAsync(factory, rewardDate: Today.AddDays(-1), streakDay: 7, amount: 30000m);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/daily-login");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<StatusResponse>();

        Assert.NotNull(payload);
        Assert.False(payload.ClaimedToday);
        // After the final day the cycle wraps: streak and amount both report day 1, not a stale 7 / day-1 mix.
        Assert.Equal(1, payload.Streak);
        Assert.Equal(1500m, payload.TodayAmount);
    }

    [Fact]
    public async Task Claim_OnConsecutiveDay_AdvancesStreak()
    {
        await using var factory = new CustomWebApplicationFactory();
        await SeedUserAndWalletAsync(factory, balance: 1000m);
        await SeedLedgerAsync(factory, rewardDate: Today.AddDays(-1), streakDay: 3, amount: 10000m);
        using var client = factory.CreateClient();

        var payload = await ClaimAsync(client);

        Assert.True(payload.Granted);
        Assert.Equal(4, payload.StreakDay);
        Assert.Equal(4500m, payload.Amount);   // default schedule day 4
        Assert.Equal(5500m, payload.NewBalance);
    }

    [Fact]
    public async Task Claim_TheDayAfterFinalDay_WrapsToDayOne()
    {
        await using var factory = new CustomWebApplicationFactory();
        await SeedUserAndWalletAsync(factory, balance: 1000m);
        await SeedLedgerAsync(factory, rewardDate: Today.AddDays(-1), streakDay: 7, amount: 30000m);
        using var client = factory.CreateClient();

        var payload = await ClaimAsync(client);

        Assert.True(payload.Granted);
        Assert.Equal(1, payload.StreakDay);
        Assert.Equal(1500m, payload.Amount);
    }

    [Fact]
    public async Task Claim_AfterMissedDay_ResetsToDayOne()
    {
        await using var factory = new CustomWebApplicationFactory();
        await SeedUserAndWalletAsync(factory, balance: 1000m);
        await SeedLedgerAsync(factory, rewardDate: Today.AddDays(-3), streakDay: 5, amount: 15000m);
        using var client = factory.CreateClient();

        var payload = await ClaimAsync(client);

        Assert.True(payload.Granted);
        Assert.Equal(1, payload.StreakDay);
        Assert.Equal(1500m, payload.Amount);
    }

    [Fact]
    public void NegativeConfiguredAmount_FailsSettingsResolution()
    {
        var overrides = new Dictionary<string, string?>
        {
            ["DailyReward:DailyAmounts:0"] = "-100"
        };

        using var factory = new CustomWebApplicationFactory(overrides);
        using var scope = factory.Services.CreateScope();

        Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IDailyRewardSettings>());
    }

    private static async Task<ClaimResponse> ClaimAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/v1/daily-login/claim", content: null);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ClaimResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private static async Task SeedUserAndWalletAsync(CustomWebApplicationFactory factory, decimal balance)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<InMemoryUserRepository>();
        var wallets = scope.ServiceProvider.GetRequiredService<InMemoryWalletRepository>();

        await users.AddAsync(new User
        {
            Id = TestUserId,
            OsuUserId = 4242,
            Username = "daily-tester",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        });

        await wallets.AddAsync(new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            Balance = balance,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        });
    }

    private static async Task SeedLedgerAsync(
        CustomWebApplicationFactory factory,
        DateOnly rewardDate,
        int streakDay,
        decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var ledger = scope.ServiceProvider.GetRequiredService<InMemoryDailyLoginRewardRepository>();

        await ledger.AddAsync(new DailyLoginReward
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            RewardDate = rewardDate,
            StreakDay = streakDay,
            Amount = amount,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        await ledger.TryCommitClaimAsync();
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
