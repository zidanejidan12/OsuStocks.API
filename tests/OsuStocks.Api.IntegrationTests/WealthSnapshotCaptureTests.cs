using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Application.Common.Models;
using OsuStocks.Application.Features.Leaderboards.CaptureWealthSnapshots;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Infrastructure.Persistence;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

/// <summary>
/// Exercises the <see cref="CaptureWealthSnapshotsCommand"/> end-to-end against the real
/// Postgres-backed handler + repository. There is no HTTP endpoint for the daily snapshot job,
/// so we resolve <see cref="ISender"/> from a factory scope and Send the command directly.
/// This validates the set-based wealth/profit aggregation SQL produces one snapshot per wallet
/// user with exact Wealth = balance + holdings value, NetDeposits = grants+rewards-deductions,
/// and Profit = Wealth - NetDeposits.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class WealthSnapshotCaptureTests(PostgresTestcontainerFixture fixture)
{
    private const decimal StockPrice = 10m;

    // Two distinct users with deterministic, exactly-computable wealth/profit math.
    //
    // UserA:
    //   wallet balance        = 500
    //   holdings              = 30 shares @ 10 = 300  -> Wealth = 800
    //   deposits              = InitialGrant 1000 + DailyReward 50 = 1050
    //   deductions            = AdminDeduction 200                 -> NetDeposits = 850
    //   Profit = 800 - 850 = -50
    private static readonly Guid UserA = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private const decimal UserABalance = 500m;
    private const int UserAShares = 30;
    private const decimal UserAWealth = UserABalance + (UserAShares * StockPrice); // 800
    private const decimal UserANetDeposits = 1000m + 50m - 200m;                   // 850
    private const decimal UserAProfit = UserAWealth - UserANetDeposits;            // -50

    // UserB:
    //   wallet balance        = 1200
    //   holdings              = 10 shares @ 10 = 100  -> Wealth = 1300
    //   deposits              = InitialGrant 1000 + AdminGrant 300 = 1300
    //   deductions            = none                                -> NetDeposits = 1300
    //   Profit = 1300 - 1300 = 0
    private static readonly Guid UserB = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private const decimal UserBBalance = 1200m;
    private const int UserBShares = 10;
    private const decimal UserBWealth = UserBBalance + (UserBShares * StockPrice); // 1300
    private const decimal UserBNetDeposits = 1000m + 300m;                         // 1300
    private const decimal UserBProfit = UserBWealth - UserBNetDeposits;            // 0

    [Fact]
    public async Task CaptureWealthSnapshots_WritesOneSnapshotPerWalletUser_WithExactWealthProfitMath()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);

        // Anchor "now" before sending so we can assert CapturedAt is recent.
        var testStart = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // A single tracked player + stock at a fixed price drives every holding's value.
            var trackedPlayer = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = 920100,
                Username = "snapshot-player",
                TrackingTier = TrackingTier.Tier1,
                IsActive = true,
                CreatedAt = testStart,
                CreatedBy = "seed"
            };
            dbContext.TrackedPlayers.Add(trackedPlayer);

            var stock = new PlayerStock
            {
                Id = Guid.NewGuid(),
                TrackedPlayerId = trackedPlayer.Id,
                CurrentPrice = StockPrice,
                DemandScore = 1m,
                PerformanceScore = 1m,
                CreatedAt = testStart,
                LastUpdated = testStart,
                CreatedBy = "seed"
            };
            dbContext.PlayerStocks.Add(stock);

            SeedUser(
                dbContext,
                userId: UserA,
                osuUserId: 920001,
                username: "snapshot-user-a",
                balance: UserABalance,
                stockId: stock.Id,
                shares: UserAShares,
                deposits:
                [
                    (WalletTransactionType.InitialGrant, 1000m),
                    (WalletTransactionType.DailyReward, 50m),
                    (WalletTransactionType.AdminDeduction, 200m)
                ],
                at: testStart);

            SeedUser(
                dbContext,
                userId: UserB,
                osuUserId: 920002,
                username: "snapshot-user-b",
                balance: UserBBalance,
                stockId: stock.Id,
                shares: UserBShares,
                deposits:
                [
                    (WalletTransactionType.InitialGrant, 1000m),
                    (WalletTransactionType.AdminGrant, 300m)
                ],
                at: testStart);

            await dbContext.SaveChangesAsync();
        }

        // Send the command via ISender (no HTTP endpoint exists for the daily job).
        Result<CaptureWealthSnapshotsResponse> result;
        using (var scope = factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            result = await sender.Send(new CaptureWealthSnapshotsCommand());
        }

        // The command succeeded and reports one snapshot per wallet user (exactly the two seeded).
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value!.SnapshotsCaptured);
        Assert.True(
            result.Value.CapturedAt >= testStart,
            $"CapturedAt {result.Value.CapturedAt:o} should be >= test start {testStart:o}.");

        // Re-open a fresh scope and read the persisted rows back from Postgres.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var snapshots = await dbContext.WealthSnapshots
                .AsNoTracking()
                .ToListAsync();

            // One row per user that has a wallet — matches the reported count.
            Assert.Equal(2, snapshots.Count);
            Assert.Equal(result.Value!.SnapshotsCaptured, snapshots.Count);

            var snapshotA = Assert.Single(snapshots, s => s.UserId == UserA);
            var snapshotB = Assert.Single(snapshots, s => s.UserId == UserB);

            // UserA exact math: Wealth=800, NetDeposits=850, Profit=-50.
            Assert.Equal(UserAWealth, snapshotA.Wealth);
            Assert.Equal(UserANetDeposits, snapshotA.NetDeposits);
            Assert.Equal(UserAProfit, snapshotA.Profit);
            Assert.Equal(snapshotA.Wealth - snapshotA.NetDeposits, snapshotA.Profit);

            // UserB exact math: Wealth=1300, NetDeposits=1300, Profit=0.
            Assert.Equal(UserBWealth, snapshotB.Wealth);
            Assert.Equal(UserBNetDeposits, snapshotB.NetDeposits);
            Assert.Equal(UserBProfit, snapshotB.Profit);
            Assert.Equal(snapshotB.Wealth - snapshotB.NetDeposits, snapshotB.Profit);

            // CapturedAt is recent (>= test start).
            Assert.All(snapshots, s => Assert.True(
                s.CapturedAt >= testStart,
                $"snapshot CapturedAt {s.CapturedAt:o} should be >= test start {testStart:o}."));

            // Both rows were written in the same capture and share an identical CapturedAt.
            Assert.Equal(snapshotA.CapturedAt, snapshotB.CapturedAt);

            // The persisted CapturedAt corresponds to the value the command reported. We compare with
            // a tolerance rather than exact equality: CapturedAt is stored as Postgres timestamptz
            // (microsecond resolution), while the reported value is an in-memory DateTimeOffset.UtcNow
            // (100ns tick resolution), so a round-trip truncates sub-microsecond ticks.
            var capturedDelta = (snapshotA.CapturedAt - result.Value.CapturedAt).Duration();
            Assert.True(
                capturedDelta <= TimeSpan.FromMilliseconds(1),
                $"persisted CapturedAt {snapshotA.CapturedAt:o} should match reported "
                + $"{result.Value.CapturedAt:o} within 1ms (delta {capturedDelta}).");
        }
    }

    /// <summary>
    /// Seeds a tradeable user: User + Wallet(balance) + Portfolio + a single Holding, plus the given
    /// wallet transactions (deposits and/or deductions). The amount stored is always positive; the
    /// aggregation subtracts <see cref="WalletTransactionType.AdminDeduction"/> from deposits.
    /// </summary>
    private static void SeedUser(
        AppDbContext dbContext,
        Guid userId,
        long osuUserId,
        string username,
        decimal balance,
        Guid stockId,
        int shares,
        (WalletTransactionType Type, decimal Amount)[] deposits,
        DateTimeOffset at)
    {
        dbContext.Users.Add(new User
        {
            Id = userId,
            OsuUserId = osuUserId,
            Username = username,
            Role = UserRole.User,
            CreatedAt = at,
            CreatedBy = "seed"
        });

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Balance = balance,
            CreatedAt = at,
            CreatedBy = "seed"
        };
        dbContext.Wallets.Add(wallet);

        foreach (var (type, amount) in deposits)
        {
            dbContext.WalletTransactions.Add(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                TransactionType = type,
                Amount = amount,
                CreatedAt = at
            });
        }

        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = at,
            CreatedBy = "seed"
        };
        dbContext.Portfolios.Add(portfolio);

        dbContext.Holdings.Add(new Holding
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolio.Id,
            StockId = stockId,
            Quantity = shares,
            AveragePrice = StockPrice,
            CreatedAt = at,
            CreatedBy = "seed"
        });
    }
}
