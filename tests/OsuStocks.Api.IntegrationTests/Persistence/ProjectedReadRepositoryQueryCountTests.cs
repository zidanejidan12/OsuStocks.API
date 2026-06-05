using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Infrastructure.Persistence;
using OsuStocks.Infrastructure.Persistence.Repositories;
using System.Data.Common;
using System.Threading;
using Xunit;

namespace OsuStocks.Api.IntegrationTests.Persistence;

public sealed class ProjectedReadRepositoryQueryCountTests
{
    [Fact]
    public async Task PortfolioReadRepository_PortfolioSummaryProjection_ExecutesSingleSelect()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var counter = new SelectCommandCounterInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var userId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();

        dbContext.Users.Add(CreateUser(userId, 910001));
        dbContext.Portfolios.Add(new Portfolio
        {
            Id = portfolioId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test"
        });

        for (var i = 0; i < 3; i++)
        {
            var trackedPlayer = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = 920000 + i,
                Username = $"player-{920000 + i}",
                TrackingTier = TrackingTier.Tier2,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            };

            var stock = new PlayerStock
            {
                Id = Guid.NewGuid(),
                TrackedPlayerId = trackedPlayer.Id,
                CurrentPrice = 100m + (i * 10m),
                DemandScore = 1m,
                PerformanceScore = 1m,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            };

            dbContext.TrackedPlayers.Add(trackedPlayer);
            dbContext.PlayerStocks.Add(stock);
            dbContext.Holdings.Add(new Holding
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolioId,
                StockId = stock.Id,
                Quantity = 2 + i,
                AveragePrice = 80m + (i * 10m),
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            });
        }

        await dbContext.SaveChangesAsync();

        var repository = new PortfolioReadRepository(dbContext);

        counter.Reset();
        var result = await repository.GetPortfolioSummaryHoldingsByUserIdAsync(userId);

        Assert.Equal(3, result.Count);
        Assert.Equal(1, counter.SelectCommandCount);
    }

    [Fact]
    public async Task PortfolioReadRepository_HoldingsProjection_ExecutesSingleSelect()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var counter = new SelectCommandCounterInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var userId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();

        dbContext.Users.Add(CreateUser(userId, 910002));
        dbContext.Portfolios.Add(new Portfolio
        {
            Id = portfolioId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test"
        });

        for (var i = 0; i < 2; i++)
        {
            var trackedPlayer = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = 930000 + i,
                Username = $"player-{930000 + i}",
                TrackingTier = TrackingTier.Tier3,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            };

            var stock = new PlayerStock
            {
                Id = Guid.NewGuid(),
                TrackedPlayerId = trackedPlayer.Id,
                CurrentPrice = 200m + (i * 50m),
                DemandScore = 1m,
                PerformanceScore = 1m,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            };

            dbContext.TrackedPlayers.Add(trackedPlayer);
            dbContext.PlayerStocks.Add(stock);
            dbContext.Holdings.Add(new Holding
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolioId,
                StockId = stock.Id,
                Quantity = 1 + i,
                AveragePrice = 150m + (i * 10m),
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            });
        }

        await dbContext.SaveChangesAsync();

        var repository = new PortfolioReadRepository(dbContext);

        counter.Reset();
        var result = await repository.GetHoldingsByUserIdAsync(userId);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, counter.SelectCommandCount);
    }

    [Fact]
    public async Task TradeReadRepository_HistoryProjection_ExecutesSingleSelect()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var counter = new SelectCommandCounterInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;

        await using var dbContext = new SqliteDateTimeOffsetAppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var userId = Guid.NewGuid();

        dbContext.Users.Add(CreateUser(userId, 910003));

        for (var i = 0; i < 5; i++)
        {
            var trackedPlayer = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = 940000 + i,
                Username = $"player-{940000 + i}",
                TrackingTier = TrackingTier.Tier1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            };

            var stock = new PlayerStock
            {
                Id = Guid.NewGuid(),
                TrackedPlayerId = trackedPlayer.Id,
                CurrentPrice = 300m + i,
                DemandScore = 1m,
                PerformanceScore = 1m,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            };

            dbContext.TrackedPlayers.Add(trackedPlayer);
            dbContext.PlayerStocks.Add(stock);
            dbContext.Trades.Add(new Trade
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StockId = stock.Id,
                TradeType = i % 2 == 0 ? TradeType.Buy : TradeType.Sell,
                Quantity = 10 + i,
                UnitPrice = 20m + i,
                TotalAmount = (20m + i) * (10 + i),
                ExecutedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
            });
        }

        await dbContext.SaveChangesAsync();

        var repository = new TradeReadRepository(dbContext);

        counter.Reset();
        var result = await repository.GetTradeHistoryByUserIdAsync(userId, 0, 50);

        Assert.Equal(5, result.Count);
        Assert.Equal(1, counter.SelectCommandCount);
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

    private sealed class SqliteDateTimeOffsetAppDbContext(DbContextOptions<AppDbContext> options)
        : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var converter = new DateTimeOffsetToBinaryConverter();

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(converter);
                    }
                    else if (property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(new ValueConverter<DateTimeOffset?, long?>(
                            v => v.HasValue ? (long?)converter.ConvertToProvider(v.Value)! : null,
                            v => v.HasValue ? (DateTimeOffset?)converter.ConvertFromProvider(v.Value)! : null));
                    }
                }
            }
        }
    }

    private sealed class SelectCommandCounterInterceptor : DbCommandInterceptor
    {
        private static readonly string[] TargetTables =
        [
            "\"holdings\"",
            "\"portfolios\"",
            "\"trades\"",
            "\"player_stocks\"",
            "\"tracked_players\""
        ];

        private int _selectCommandCount;

        public int SelectCommandCount => Volatile.Read(ref _selectCommandCount);

        public void Reset()
        {
            Interlocked.Exchange(ref _selectCommandCount, 0);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Count(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Count(DbCommand command)
        {
            if (!command.CommandText.Contains("select", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!TargetTables.Any(table => command.CommandText.Contains(table, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            Interlocked.Increment(ref _selectCommandCount);
        }
    }
}

