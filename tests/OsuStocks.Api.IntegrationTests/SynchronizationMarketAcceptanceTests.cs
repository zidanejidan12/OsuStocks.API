using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Application.Features.Market.Services;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Market.Events;
using OsuStocks.Domain.Market.Models;
using OsuStocks.Infrastructure.Persistence;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class MarketEngineIntegrationTests(PostgresTestcontainerFixture fixture)
{
    [Fact]
    public async Task ApplyForStock_BuyOrder_UpdatesPriceAndPersistsHistory()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid stockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var trackedPlayer = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = 1001,
                Username = "mrekk",
                TrackingTier = TrackingTier.Tier1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };
            dbContext.TrackedPlayers.Add(trackedPlayer);

            var stock = new PlayerStock
            {
                Id = Guid.NewGuid(),
                TrackedPlayerId = trackedPlayer.Id,
                CurrentPrice = 100m,
                DemandScore = 1m,
                PerformanceScore = 1m,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };

            dbContext.PlayerStocks.Add(stock);
            await dbContext.SaveChangesAsync();

            stockId = stock.Id;
        }

        PriceChanged? changed;
        var occurredAt = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var marketService = scope.ServiceProvider.GetRequiredService<IMarketEventProcessingService>();
            changed = await marketService.ApplyForStockAsync(
                stockId,
                MarketPriceInput.Buy(3),
                occurredAt,
                CancellationToken.None);
        }

        Assert.NotNull(changed);
        Assert.Equal(PriceChangeReason.BuyPressure, changed!.Reason);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var updatedStock = await dbContext.PlayerStocks
                .AsNoTracking()
                .SingleAsync(x => x.Id == stockId);

            Assert.Equal(changed.NewPrice, updatedStock.CurrentPrice);
            // PostgreSQL timestamptz stores microsecond precision; .NET DateTimeOffset keeps 100ns ticks.
            // Compare with a 1-microsecond tolerance to account for the round-trip truncation.
            Assert.Equal(occurredAt, updatedStock.LastUpdated, TimeSpan.FromTicks(10));

            var history = await dbContext.StockPriceHistory
                .AsNoTracking()
                .Where(x => x.StockId == stockId)
                .ToListAsync();

            var entry = Assert.Single(history);
            Assert.Equal(PriceChangeReason.BuyPressure, entry.Reason);
            Assert.Equal(changed.PreviousPrice, entry.PreviousPrice);
            Assert.Equal(changed.NewPrice, entry.NewPrice);
        }
    }

    [Fact]
    public async Task ApplyForTrackedPlayer_Inactivity_RespectsPriceFloor()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid trackedPlayerId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var trackedPlayer = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = 1002,
                Username = "whitecat",
                TrackingTier = TrackingTier.Tier1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };
            dbContext.TrackedPlayers.Add(trackedPlayer);

            dbContext.PlayerStocks.Add(new PlayerStock
            {
                Id = Guid.NewGuid(),
                TrackedPlayerId = trackedPlayer.Id,
                CurrentPrice = 1.2m,
                DemandScore = 1m,
                PerformanceScore = 1m,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            });

            await dbContext.SaveChangesAsync();
            trackedPlayerId = trackedPlayer.Id;
        }

        PriceChanged? changed;

        using (var scope = factory.Services.CreateScope())
        {
            var marketService = scope.ServiceProvider.GetRequiredService<IMarketEventProcessingService>();
            changed = await marketService.ApplyForTrackedPlayerAsync(
                trackedPlayerId,
                MarketPriceInput.Inactivity(),
                DateTimeOffset.UtcNow,
                CancellationToken.None);
        }

        Assert.NotNull(changed);
        Assert.Equal(PriceChangeReason.Decay, changed!.Reason);
        // Seeded 1.2 with the production 0.5% inactivity decay -> 1.2 * 0.995 = 1.1940, which is above
        // the price floor of 1, so no clamping occurs here. (Floor clamping itself is covered by the
        // unit test MarketPriceEngineTests.Calculate_Inactivity_EnforcesPriceFloor, which uses a 50% decay.)
        Assert.Equal(1.194m, changed.NewPrice);
    }
}

