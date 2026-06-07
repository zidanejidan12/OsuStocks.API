using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.OsuIntegration.Events;
using OsuStocks.Infrastructure.Persistence;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

/// <summary>
/// Exercises the market-event -> notification fan-out end-to-end against the real Postgres-backed
/// notification handler + repository. There is no HTTP endpoint for fan-out, so we resolve
/// <see cref="IPublisher"/> from a factory scope and Publish the market notification directly.
/// Only users that currently hold a positive quantity of the affected stock should receive a row.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class NotificationFanoutTests(PostgresTestcontainerFixture fixture)
{
    private const decimal StockPrice = 100m;

    // Two holders of the affected stock and a third user with no holding.
    private static readonly Guid HolderOne = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid HolderTwo = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid NonHolder = Guid.Parse("dddddddd-0000-0000-0000-000000000003");

    [Fact]
    public async Task PpIncreased_FansOutNotificationsToHoldersOnly()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);

        var at = DateTimeOffset.UtcNow;
        Guid trackedPlayerId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seed = SeedStockWithHolders(dbContext, osuUserId: 930100, username: "fanout-player-pp", at: at);
            trackedPlayerId = seed.TrackedPlayerId;
            await dbContext.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(new PpIncreasedNotification(
                new PpIncreased(
                    TrackedPlayerId: trackedPlayerId,
                    PreviousPp: 1000m,
                    CurrentPp: 1100m,
                    OccurredAt: at)));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notifications = await dbContext.Set<Notification>()
                .AsNoTracking()
                .ToListAsync();

            // Exactly the two holders received a row; the non-holder received none.
            Assert.Equal(2, notifications.Count);
            Assert.Contains(notifications, n => n.UserId == HolderOne);
            Assert.Contains(notifications, n => n.UserId == HolderTwo);
            Assert.DoesNotContain(notifications, n => n.UserId == NonHolder);

            // Every fan-out row is of the PpIncreased type and starts unread.
            Assert.All(notifications, n =>
            {
                Assert.Equal("PpIncreased", n.Type);
                Assert.False(n.IsRead);
            });
        }
    }

    [Fact]
    public async Task TopPlayDetected_FansOutNotificationsToHoldersOnly()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);

        var at = DateTimeOffset.UtcNow;
        Guid trackedPlayerId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seed = SeedStockWithHolders(dbContext, osuUserId: 930200, username: "fanout-player-top", at: at);
            trackedPlayerId = seed.TrackedPlayerId;
            await dbContext.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(new TopPlayDetectedNotification(
                new TopPlayDetected(
                    TrackedPlayerId: trackedPlayerId,
                    PreviousTopScoreId: 5001,
                    NewTopScoreId: 5002,
                    NewTopScorePp: 1234m,
                    OccurredAt: at)));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notifications = await dbContext.Set<Notification>()
                .AsNoTracking()
                .Where(n => n.Type == "TopPlayDetected")
                .ToListAsync();

            // Exactly the two holders received a TopPlayDetected row; the non-holder received none.
            Assert.Equal(2, notifications.Count);
            Assert.Contains(notifications, n => n.UserId == HolderOne);
            Assert.Contains(notifications, n => n.UserId == HolderTwo);
            Assert.DoesNotContain(notifications, n => n.UserId == NonHolder);
            Assert.All(notifications, n => Assert.False(n.IsRead));
        }
    }

    /// <summary>
    /// Seeds one tracked player + stock, two users that each hold a positive quantity of that stock,
    /// and a third user with a portfolio but no holding in it. Returns the tracked player id used to
    /// publish the market event.
    /// </summary>
    private static (Guid TrackedPlayerId, Guid StockId) SeedStockWithHolders(
        AppDbContext dbContext,
        long osuUserId,
        string username,
        DateTimeOffset at)
    {
        var trackedPlayer = new TrackedPlayer
        {
            Id = Guid.NewGuid(),
            OsuUserId = osuUserId,
            Username = username,
            TrackingTier = TrackingTier.Tier1,
            IsActive = true,
            CreatedAt = at,
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
            CreatedAt = at,
            LastUpdated = at,
            CreatedBy = "seed"
        };
        dbContext.PlayerStocks.Add(stock);

        // Two holders with a positive quantity in the affected stock.
        SeedHolder(dbContext, HolderOne, osuUserId: osuUserId + 1, username: $"{username}-holder-1", stockId: stock.Id, quantity: 3, at: at);
        SeedHolder(dbContext, HolderTwo, osuUserId: osuUserId + 2, username: $"{username}-holder-2", stockId: stock.Id, quantity: 5, at: at);

        // A third user with a portfolio but no holding in this stock.
        SeedNonHolder(dbContext, NonHolder, osuUserId: osuUserId + 3, username: $"{username}-nonholder", at: at);

        return (trackedPlayer.Id, stock.Id);
    }

    private static void SeedHolder(
        AppDbContext dbContext,
        Guid userId,
        long osuUserId,
        string username,
        Guid stockId,
        int quantity,
        DateTimeOffset at)
    {
        dbContext.Users.Add(CreateUser(userId, osuUserId, username, at));

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
            Quantity = quantity,
            AveragePrice = StockPrice,
            CreatedAt = at,
            CreatedBy = "seed"
        });
    }

    private static void SeedNonHolder(
        AppDbContext dbContext,
        Guid userId,
        long osuUserId,
        string username,
        DateTimeOffset at)
    {
        dbContext.Users.Add(CreateUser(userId, osuUserId, username, at));

        // A portfolio with no holdings: this user must not receive a fan-out notification.
        dbContext.Portfolios.Add(new Portfolio
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = at,
            CreatedBy = "seed"
        });
    }

    private static User CreateUser(Guid userId, long osuUserId, string username, DateTimeOffset at)
    {
        return new User
        {
            Id = userId,
            OsuUserId = osuUserId,
            Username = username,
            Role = UserRole.User,
            CreatedAt = at,
            CreatedBy = "seed"
        };
    }
}
