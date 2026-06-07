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
/// Integration coverage for GET /api/v1/market/stocks/{id}/history. Exercises the raw-Postgres
/// date_trunc OHLC path (range=...) against a real Testcontainer database, plus the back-compat
/// flat price list (no range), the validator's 400 on an unsupported range, and the 404 for an
/// unknown stock. Timestamps are seeded in UTC and floored to whole-minute boundaries so the
/// 1-minute (range=1h) buckets are deterministic.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class MarketCandlesEndpointTests(PostgresTestcontainerFixture fixture)
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetHistory_WithRange1h_ReturnsOhlcCandlesWithVolume_OrderedByBucketStartAsc()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        // Anchor the seeded data ~30 minutes in the past, floored to a whole minute. This keeps every
        // point comfortably inside the [now-1h, now] window the handler computes while landing rows on
        // exact, predictable 1-minute bucket boundaries.
        var bucketA = FloorToMinute(DateTimeOffset.UtcNow.AddMinutes(-30));
        var bucketB = bucketA.AddMinutes(1);

        Guid stockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(TestUserId, 730001));

            var trackedPlayer = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = 920001,
                Username = "candle-player",
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
                CurrentPrice = 12m,
                DemandScore = 1m,
                PerformanceScore = 1m,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };
            dbContext.PlayerStocks.Add(stock);
            stockId = stock.Id;

            // Bucket A: four price points within the SAME 1-minute bucket, deliberately out of price
            // order but in a fixed created_at order so OHLC is unambiguous:
            //   open  = first by created_at = 10
            //   high  = max                 = 15
            //   low   = min                 = 8
            //   close = last by created_at  = 12
            dbContext.StockPriceHistory.AddRange(
                PriceHistory(stockId, previous: 9m, next: 10m, at: bucketA.AddSeconds(5)),
                PriceHistory(stockId, previous: 10m, next: 15m, at: bucketA.AddSeconds(20)),
                PriceHistory(stockId, previous: 15m, next: 8m, at: bucketA.AddSeconds(35)),
                PriceHistory(stockId, previous: 8m, next: 12m, at: bucketA.AddSeconds(50)));

            // Bucket B: a single price point in the NEXT 1-minute bucket. Single point => O=H=L=C=20.
            dbContext.StockPriceHistory.Add(
                PriceHistory(stockId, previous: 12m, next: 20m, at: bucketB.AddSeconds(10)));

            // Trades drive the volume column (SUM of quantity per bucket).
            //   Bucket A: 3 + 7 = 10
            //   Bucket B: 5
            dbContext.Trades.AddRange(
                Trade(stockId, quantity: 3, at: bucketA.AddSeconds(8)),
                Trade(stockId, quantity: 7, at: bucketA.AddSeconds(40)),
                Trade(stockId, quantity: 5, at: bucketB.AddSeconds(15)));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/v1/market/stocks/{stockId}/history?range=1h");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CandlesEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal("1h", payload.Range);
        Assert.NotNull(payload.Candles);
        Assert.Equal(2, payload.Candles.Count);

        // Buckets are epoch-aligned UTC: bucket_start = to_timestamp(floor(epoch/width)*width). With a
        // 1-minute width, a row at bucketA.AddSeconds(5..50) floors to exactly bucketA, and the next
        // minute's row to bucketB. So the absolute boundaries are deterministic and clean, candles are
        // ordered ascending, and adjacent 1-minute buckets are exactly 60s apart.
        Assert.Equal(bucketA, payload.Candles[0].BucketStart);
        Assert.Equal(bucketB, payload.Candles[1].BucketStart);
        Assert.Equal(TimeSpan.Zero, payload.Candles[0].BucketStart.Offset);
        Assert.Equal(TimeSpan.FromMinutes(1), payload.Candles[1].BucketStart - payload.Candles[0].BucketStart);

        // Bucket A aggregates the four same-minute price points:
        //   open = first by created_at (10), close = last by created_at (12), high = max (15), low = min (8).
        // Volume = SUM(quantity) of the two bucket-A trades (3 + 7 = 10).
        var first = payload.Candles[0];
        Assert.Equal(10m, first.Open);
        Assert.Equal(15m, first.High);
        Assert.Equal(8m, first.Low);
        Assert.Equal(12m, first.Close);
        Assert.Equal(10, first.Volume);

        // Bucket B has a single price point (O=H=L=C=20) and one trade (volume 5).
        var second = payload.Candles[1];
        Assert.Equal(20m, second.Open);
        Assert.Equal(20m, second.High);
        Assert.Equal(20m, second.Low);
        Assert.Equal(20m, second.Close);
        Assert.Equal(5, second.Volume);
    }

    [Fact]
    public async Task GetHistory_WithRange24h_GroupsMultipleMinutesIntoOne30MinuteBucket()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        // Epoch-aligned 30-minute buckets fall on :00 and :30 UTC. An hour boundary is also a 30-min
        // boundary, so three points at hour+2/+12/+25 min all share ONE 30-minute bucket. This is the
        // case the earlier (date_trunc + offset) bucketing got wrong by splitting per-minute.
        var slot = FloorToHour(DateTimeOffset.UtcNow.AddHours(-2));

        Guid stockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(TestUserId, 730005));

            var trackedPlayer = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = 920005,
                Username = "candle-player-30m",
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
                CurrentPrice = 50m,
                DemandScore = 1m,
                PerformanceScore = 1m,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };
            dbContext.PlayerStocks.Add(stock);
            stockId = stock.Id;

            // Three points across different minutes but the SAME 30-minute bucket.
            // open = first by created_at = 50, close = last = 40, high = 70, low = 40.
            dbContext.StockPriceHistory.AddRange(
                PriceHistory(stockId, previous: 48m, next: 50m, at: slot.AddMinutes(2)),
                PriceHistory(stockId, previous: 50m, next: 70m, at: slot.AddMinutes(12)),
                PriceHistory(stockId, previous: 70m, next: 40m, at: slot.AddMinutes(25)));

            dbContext.Trades.Add(Trade(stockId, quantity: 9, at: slot.AddMinutes(5)));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/v1/market/stocks/{stockId}/history?range=24h");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CandlesEnvelope>();
        Assert.NotNull(payload);
        Assert.Equal("24h", payload.Range);
        Assert.NotNull(payload.Candles);

        // The three minutes must collapse into exactly ONE 30-minute candle at the slot boundary.
        Assert.Single(payload.Candles);
        var candle = payload.Candles[0];
        Assert.Equal(slot, candle.BucketStart);
        Assert.Equal(50m, candle.Open);
        Assert.Equal(70m, candle.High);
        Assert.Equal(40m, candle.Low);
        Assert.Equal(40m, candle.Close);
        Assert.Equal(9, candle.Volume);
    }

    [Fact]
    public async Task GetHistory_WithUnsupportedRange_ReturnsBadRequest()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid stockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(TestUserId, 730002));

            var trackedPlayer = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = 920002,
                Username = "candle-player-bad-range",
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
                CurrentPrice = 5m,
                DemandScore = 1m,
                PerformanceScore = 1m,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };
            dbContext.PlayerStocks.Add(stock);
            stockId = stock.Id;

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/v1/market/stocks/{stockId}/history?range=99x");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_WithoutRange_ReturnsFlatPriceList_ForBackCompat()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        var pointOne = FloorToMinute(DateTimeOffset.UtcNow.AddHours(-2));
        var pointTwo = pointOne.AddHours(1);

        Guid stockId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.Add(CreateUser(TestUserId, 730003));

            var trackedPlayer = new TrackedPlayer
            {
                Id = Guid.NewGuid(),
                OsuUserId = 920003,
                Username = "candle-player-flat",
                TrackingTier = TrackingTier.Tier3,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };
            dbContext.TrackedPlayers.Add(trackedPlayer);

            var stock = new PlayerStock
            {
                Id = Guid.NewGuid(),
                TrackedPlayerId = trackedPlayer.Id,
                CurrentPrice = 7m,
                DemandScore = 1m,
                PerformanceScore = 1m,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow,
                CreatedBy = "seed"
            };
            dbContext.PlayerStocks.Add(stock);
            stockId = stock.Id;

            dbContext.StockPriceHistory.AddRange(
                PriceHistory(stockId, previous: 5m, next: 6m, at: pointOne),
                PriceHistory(stockId, previous: 6m, next: 7m, at: pointTwo));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/v1/market/stocks/{stockId}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Back-compat shape: a flat array of { timestamp, price } (NOT the candles envelope), ordered
        // by created_at ascending.
        var payload = await response.Content.ReadFromJsonAsync<List<FlatHistoryPoint>>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Count);
        Assert.Equal(6m, payload[0].Price);
        Assert.Equal(7m, payload[1].Price);
        Assert.True(payload[0].Timestamp < payload[1].Timestamp);
    }

    [Fact]
    public async Task GetHistory_WithRange_ForUnknownStock_ReturnsNotFound()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        // Seed a valid user so the request is authenticated cleanly, but never create the stock we query.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Users.Add(CreateUser(TestUserId, 730004));
            await dbContext.SaveChangesAsync();
        }

        var unknownStockId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/v1/market/stocks/{unknownStockId}/history?range=1h");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("NOT_FOUND", error.Code);
    }

    private static DateTimeOffset FloorToMinute(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, TimeSpan.Zero);

    private static DateTimeOffset FloorToHour(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, value.Hour, 0, 0, TimeSpan.Zero);

    private static StockPriceHistory PriceHistory(Guid stockId, decimal previous, decimal next, DateTimeOffset at) =>
        new()
        {
            Id = Guid.NewGuid(),
            StockId = stockId,
            PreviousPrice = previous,
            NewPrice = next,
            Reason = PriceChangeReason.BuyPressure,
            CreatedAt = at
        };

    private static Trade Trade(Guid stockId, int quantity, DateTimeOffset at) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            StockId = stockId,
            TradeType = TradeType.Buy,
            Quantity = quantity,
            UnitPrice = 1m,
            TotalAmount = quantity * 1m,
            ExecutedAt = at
        };

    private static User CreateUser(Guid userId, long osuUserId) =>
        new()
        {
            Id = userId,
            OsuUserId = osuUserId,
            Username = $"user-{osuUserId}",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed"
        };

    private sealed record CandlesEnvelope(
        [property: JsonPropertyName("range")] string Range,
        [property: JsonPropertyName("candles")] List<CandleEnvelope> Candles);

    private sealed record CandleEnvelope(
        [property: JsonPropertyName("bucketStart")] DateTimeOffset BucketStart,
        [property: JsonPropertyName("open")] decimal Open,
        [property: JsonPropertyName("high")] decimal High,
        [property: JsonPropertyName("low")] decimal Low,
        [property: JsonPropertyName("close")] decimal Close,
        [property: JsonPropertyName("volume")] long Volume);

    private sealed record FlatHistoryPoint(
        [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
        [property: JsonPropertyName("price")] decimal Price);

    private sealed record ErrorResponse(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message);
}
