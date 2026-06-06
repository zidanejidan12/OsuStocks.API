using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.Repositories;
using OsuStocks.Infrastructure.Persistence;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class GlobalExceptionHandlerTests(PostgresTestcontainerFixture fixture)
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ConcurrencyConflict_Returns409WithConcurrencyConflictCode()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid stockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(new User
            {
                Id = TestUserId,
                OsuUserId = 601001,
                Username = "concurrency-user",
                Role = UserRole.User,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            });

            dbContext.Wallets.Add(new Wallet
            {
                Id = Guid.NewGuid(),
                UserId = TestUserId,
                Balance = 1_000_000m,
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
                OsuUserId = 601,
                Username = "player-601",
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
                CurrentPrice = 10m,
                DemandScore = 0m,
                PerformanceScore = 0m,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };
            dbContext.PlayerStocks.Add(stock);
            stockId = stock.Id;

            await dbContext.SaveChangesAsync();
        }

        // Simulate a concurrency conflict by tampering with the wallet row_version
        // between the handler's read and the SaveChangesAsync.
        // First, do a normal buy to confirm it works.
        var firstBuy = await client.PostAsJsonAsync(
            "/api/v1/trading/buy",
            new { stockId, quantity = 1 });
        firstBuy.EnsureSuccessStatusCode();

        // Now corrupt the row_version for the wallet so the next save will conflict.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE wallets SET row_version = row_version + 100 WHERE user_id = {0}",
                TestUserId);
        }

        var conflictBuy = await client.PostAsJsonAsync(
            "/api/v1/trading/buy",
            new { stockId, quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, conflictBuy.StatusCode);

        var error = await conflictBuy.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("CONCURRENCY_CONFLICT", error.Code);
        Assert.NotNull(error.TraceId);
    }

    [Fact]
    public async Task HealthEndpoint_Returns200()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record ErrorResponse(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("traceId")] string? TraceId);
}
