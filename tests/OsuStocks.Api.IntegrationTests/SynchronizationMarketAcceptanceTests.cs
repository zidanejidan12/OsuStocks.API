using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.Market.EventHandlers;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Application.Features.Market.Services;
using OsuStocks.Application.Features.OsuIntegration.Synchronization.Services;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Market.Models;
using OsuStocks.Domain.Market.Services;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class SynchronizationMarketAcceptanceTests
{
    [Fact]
    public async Task Synchronization_EmitsSignals_PersistsSnapshotsAndEvents_AndUpdatesPricesWithHistoryReasons()
    {
        var now = DateTimeOffset.UtcNow;

        var trackedPlayerRepository = new InMemoryTrackedPlayerRepository();
        var playerSnapshotRepository = new InMemoryPlayerSnapshotRepository();
        var playerStockRepository = new InMemoryPlayerStockRepository();
        var marketEventRepository = new InMemoryMarketEventRepository();
        var stockPriceHistoryRepository = new InMemoryStockPriceHistoryRepository();

        var playerA = CreateTrackedPlayer(1001, "mrekk", TrackingTier.Tier1, true);
        var playerB = CreateTrackedPlayer(1002, "whitecat", TrackingTier.Tier1, true);

        await trackedPlayerRepository.AddAsync(playerA);
        await trackedPlayerRepository.AddAsync(playerB);

        await playerStockRepository.AddAsync(CreateStock(playerA.Id, 100m, now.AddDays(-1)));
        await playerStockRepository.AddAsync(CreateStock(playerB.Id, 1.2m, now.AddDays(-15)));

        await playerSnapshotRepository.AddAsync(new PlayerSnapshot
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = playerA.Id,
            CurrentPp = 10000m,
            GlobalRank = 500,
            TopScoreId = 11,
            TopScorePp = 1000m,
            CapturedAt = now.AddDays(-1)
        });

        await playerSnapshotRepository.AddAsync(new PlayerSnapshot
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = playerB.Id,
            CurrentPp = 9000m,
            GlobalRank = 700,
            TopScoreId = 22,
            TopScorePp = 800m,
            CapturedAt = now.AddDays(-15)
        });

        var marketService = new MarketEventProcessingService(
            playerStockRepository,
            stockPriceHistoryRepository,
            new NoOpApplicationDbContext(),
            new StaticMarketCoefficientsProvider(new MarketPricingCoefficients(
                0.0025m,
                0.0025m,
                0.03m,
                0.0002m,
                0.10m,
                0.50m,
                1m)),
            new MarketPriceEngine());

        var publisher = new MarketEventRelayPublisher(marketService);

        var synchronizationService = new PlayerSynchronizationService(
            new FakeOsuOAuthService(),
            new ConfigurableOsuApiClient(new Dictionary<long, OsuUserProfile>
            {
                [1001] = new(1001, "mrekk", "https://avatar.example/mrekk", 10100m, 450, 12, 1010m),
                [1002] = new(1002, "whitecat", "https://avatar.example/whitecat", 9000m, 700, 22, 800m)
            }),
            new SnapshotComparisonService(),
            trackedPlayerRepository,
            playerSnapshotRepository,
            playerStockRepository,
            marketEventRepository,
            new NoOpApplicationDbContext(),
            publisher);

        var summary = await synchronizationService.SynchronizeTrackedPlayersAsync(TrackingTier.Tier1);

        Assert.Equal(2, summary.TrackedPlayers);
        Assert.Equal(2, summary.SnapshotsCreated);
        Assert.Equal(3, summary.EventsDetected);
        Assert.Equal(1, summary.RankImprovementsDetected);

        var marketEvents = marketEventRepository.GetAll();
        Assert.Contains(marketEvents, x => x.EventType == "PpIncreased");
        Assert.Contains(marketEvents, x => x.EventType == "TopPlayDetected");
        Assert.Contains(marketEvents, x => x.EventType == "PlayerInactive");

        var stockA = await playerStockRepository.GetByTrackedPlayerIdAsync(playerA.Id);
        var stockB = await playerStockRepository.GetByTrackedPlayerIdAsync(playerB.Id);

        Assert.NotNull(stockA);
        Assert.NotNull(stockB);
        Assert.True(stockA.CurrentPrice > 100m);
        Assert.True(stockB.CurrentPrice >= 1m);

        var stockAHistory = stockPriceHistoryRepository.GetAllForStock(stockA.Id);
        var stockBHistory = stockPriceHistoryRepository.GetAllForStock(stockB.Id);

        Assert.Contains(stockAHistory, x => x.Reason == PriceChangeReason.PPGain);
        Assert.Contains(stockAHistory, x => x.Reason == PriceChangeReason.TopPlay);
        Assert.Contains(stockBHistory, x => x.Reason == PriceChangeReason.Decay);

        Assert.Equal(3, publisher.PriceChangedNotifications.Count);
    }

    private static TrackedPlayer CreateTrackedPlayer(long osuUserId, string username, TrackingTier tier, bool isActive)
    {
        return new TrackedPlayer
        {
            Id = Guid.NewGuid(),
            OsuUserId = osuUserId,
            Username = username,
            TrackingTier = tier,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test"
        };
    }

    private static PlayerStock CreateStock(Guid trackedPlayerId, decimal currentPrice, DateTimeOffset lastUpdated)
    {
        return new PlayerStock
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = trackedPlayerId,
            CurrentPrice = currentPrice,
            DemandScore = 0m,
            PerformanceScore = 0m,
            CreatedAt = lastUpdated,
            LastUpdated = lastUpdated,
            CreatedBy = "test"
        };
    }

    private sealed class ConfigurableOsuApiClient(IReadOnlyDictionary<long, OsuUserProfile> users) : IOsuApiClient
    {
        public Task<OsuUserProfile> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            var first = users.Values.FirstOrDefault()
                        ?? throw new InvalidOperationException("No users configured.");
            return Task.FromResult(first);
        }

        public Task<OsuUserProfile> GetUserAsync(long osuUserId, string accessToken, CancellationToken cancellationToken = default)
        {
            if (!users.TryGetValue(osuUserId, out var user))
            {
                throw new HttpRequestException($"osu user '{osuUserId}' not configured.");
            }

            return Task.FromResult(user);
        }

        public Task<IReadOnlyList<OsuUserProfile>> SearchUsersAsync(
            string query,
            string accessToken,
            int limit = 10,
            CancellationToken cancellationToken = default)
        {
            var items = users.Values
                .Where(x => x.Username.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<OsuUserProfile>>(items);
        }
    }

    private sealed class StaticMarketCoefficientsProvider(MarketPricingCoefficients coefficients) : IMarketCoefficientsProvider
    {
        public Task<MarketPricingCoefficients> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(coefficients);
        }
    }

    private sealed class MarketEventRelayPublisher : IPublisher
    {
        private readonly PpIncreasedEventHandler _ppIncreasedHandler;
        private readonly TopPlayDetectedEventHandler _topPlayHandler;
        private readonly PlayerInactiveEventHandler _inactiveHandler;

        public List<PriceChangedNotification> PriceChangedNotifications { get; } = [];

        public MarketEventRelayPublisher(IMarketEventProcessingService processingService)
        {
            _ppIncreasedHandler = new PpIncreasedEventHandler(processingService, this);
            _topPlayHandler = new TopPlayDetectedEventHandler(processingService, this);
            _inactiveHandler = new PlayerInactiveEventHandler(processingService, this);
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            return notification switch
            {
                PpIncreasedNotification pp => _ppIncreasedHandler.Handle(pp, cancellationToken),
                TopPlayDetectedNotification topPlay => _topPlayHandler.Handle(topPlay, cancellationToken),
                PlayerInactiveNotification inactive => _inactiveHandler.Handle(inactive, cancellationToken),
                PriceChangedNotification changed => CapturePriceChangedAsync(changed),
                _ => Task.CompletedTask
            };
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Publish((object)notification, cancellationToken);
        }

        private Task CapturePriceChangedAsync(PriceChangedNotification changed)
        {
            PriceChangedNotifications.Add(changed);
            return Task.CompletedTask;
        }
    }
}
