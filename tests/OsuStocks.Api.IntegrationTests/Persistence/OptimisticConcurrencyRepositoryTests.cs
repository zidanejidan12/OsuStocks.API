using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Infrastructure.Persistence;
using OsuStocks.Infrastructure.Persistence.Repositories;
using Xunit;

namespace OsuStocks.Api.IntegrationTests.Persistence;

public sealed class OptimisticConcurrencyRepositoryTests
{
    [Fact]
    public async Task WalletRepository_StaleRowVersion_ThrowsConcurrencyException()
    {
        await using var connection = CreateInMemoryConnection();
        var options = CreateOptions(connection);

        var userId = Guid.NewGuid();
        var walletId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.Users.Add(CreateUser(userId, 1001));
            seedContext.Wallets.Add(new Wallet
            {
                Id = walletId,
                UserId = userId,
                Balance = 1_000m,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            });

            await seedContext.SaveChangesAsync();
        }

        await using var contextA = new AppDbContext(options);
        await using var contextB = new AppDbContext(options);

        var repositoryA = new WalletRepository(contextA);
        var repositoryB = new WalletRepository(contextB);

        var walletA = await repositoryA.GetByUserIdAsync(userId);
        var walletB = await repositoryB.GetByUserIdAsync(userId);

        Assert.NotNull(walletA);
        Assert.NotNull(walletB);

        walletA!.Balance -= 100m;
        walletA.UpdatedAt = DateTimeOffset.UtcNow;
        repositoryA.Update(walletA);

        walletB!.Balance -= 50m;
        walletB.UpdatedAt = DateTimeOffset.UtcNow;
        repositoryB.Update(walletB);

        await contextA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
        {
            await contextB.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task HoldingRepository_StaleRowVersion_ThrowsConcurrencyException()
    {
        await using var connection = CreateInMemoryConnection();
        var options = CreateOptions(connection);

        var userId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var trackedPlayerId = Guid.NewGuid();
        var stockId = Guid.NewGuid();
        var holdingId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Database.EnsureCreatedAsync();

            seedContext.Users.Add(CreateUser(userId, 1002));
            seedContext.Portfolios.Add(new Portfolio
            {
                Id = portfolioId,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            });

            seedContext.TrackedPlayers.Add(CreateTrackedPlayer(trackedPlayerId, 2001));
            seedContext.PlayerStocks.Add(new PlayerStock
            {
                Id = stockId,
                TrackedPlayerId = trackedPlayerId,
                CurrentPrice = 10m,
                DemandScore = 1m,
                PerformanceScore = 1m,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            });

            seedContext.Holdings.Add(new Holding
            {
                Id = holdingId,
                PortfolioId = portfolioId,
                StockId = stockId,
                Quantity = 10,
                AveragePrice = 10m,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            });

            await seedContext.SaveChangesAsync();
        }

        await using var contextA = new AppDbContext(options);
        await using var contextB = new AppDbContext(options);

        var repositoryA = new HoldingRepository(contextA);
        var repositoryB = new HoldingRepository(contextB);

        var holdingA = await repositoryA.GetByPortfolioAndStockAsync(portfolioId, stockId);
        var holdingB = await repositoryB.GetByPortfolioAndStockAsync(portfolioId, stockId);

        Assert.NotNull(holdingA);
        Assert.NotNull(holdingB);

        holdingA!.Quantity += 5;
        holdingA.UpdatedAt = DateTimeOffset.UtcNow;
        repositoryA.Update(holdingA);

        holdingB!.Quantity += 1;
        holdingB.UpdatedAt = DateTimeOffset.UtcNow;
        repositoryB.Update(holdingB);

        await contextA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
        {
            await contextB.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task PlayerStockRepository_StaleRowVersion_ThrowsConcurrencyException()
    {
        await using var connection = CreateInMemoryConnection();
        var options = CreateOptions(connection);

        var trackedPlayerId = Guid.NewGuid();
        var stockId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Database.EnsureCreatedAsync();

            seedContext.TrackedPlayers.Add(CreateTrackedPlayer(trackedPlayerId, 2002));
            seedContext.PlayerStocks.Add(new PlayerStock
            {
                Id = stockId,
                TrackedPlayerId = trackedPlayerId,
                CurrentPrice = 20m,
                DemandScore = 1m,
                PerformanceScore = 1m,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            });

            await seedContext.SaveChangesAsync();
        }

        await using var contextA = new AppDbContext(options);
        await using var contextB = new AppDbContext(options);

        var repositoryA = new PlayerStockRepository(contextA);
        var repositoryB = new PlayerStockRepository(contextB);

        var stockA = await repositoryA.GetByIdAsync(stockId);
        var stockB = await repositoryB.GetByIdAsync(stockId);

        Assert.NotNull(stockA);
        Assert.NotNull(stockB);

        stockA!.CurrentPrice = 21m;
        stockA.LastUpdated = DateTimeOffset.UtcNow;
        repositoryA.Update(stockA);

        stockB!.CurrentPrice = 22m;
        stockB.LastUpdated = DateTimeOffset.UtcNow;
        repositoryB.Update(stockB);

        await contextA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
        {
            await contextB.SaveChangesAsync();
        });
    }

    private static SqliteConnection CreateInMemoryConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static DbContextOptions<AppDbContext> CreateOptions(SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
    }

    private static User CreateUser(Guid userId, long osuUserId)
    {
        return new User
        {
            Id = userId,
            OsuUserId = osuUserId,
            Username = $"user-{osuUserId}",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test"
        };
    }

    private static TrackedPlayer CreateTrackedPlayer(Guid trackedPlayerId, long osuUserId)
    {
        return new TrackedPlayer
        {
            Id = trackedPlayerId,
            OsuUserId = osuUserId,
            Username = $"player-{osuUserId}",
            TrackingTier = TrackingTier.Tier3,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test"
        };
    }
}
