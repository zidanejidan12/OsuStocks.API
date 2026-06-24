using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.OsuIntegration.Synchronization.Services;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class SynchronizationWorkerIntegrationTests
{
    [Fact]
    public async Task SynchronizeTier1_PersistsSnapshots_DetectsSignals_AndDoesNotUpdateStockPrices()
    {
        var now = DateTimeOffset.UtcNow;

        var trackedPlayerRepository = new InMemoryTrackedPlayerRepository();
        var playerSnapshotRepository = new InMemoryPlayerSnapshotRepository();
        var playerStockRepository = new InMemoryPlayerStockRepository();
        var marketEventRepository = new InMemoryMarketEventRepository();

        var tier1A = CreateTrackedPlayer(1001, "mrekk", TrackingTier.Tier1, isActive: true);
        var tier1B = CreateTrackedPlayer(1002, "whitecat", TrackingTier.Tier1, isActive: true);
        var tier1Disabled = CreateTrackedPlayer(1003, "vaxei", TrackingTier.Tier1, isActive: false);
        var tier2Active = CreateTrackedPlayer(1004, "other", TrackingTier.Tier2, isActive: true);

        await trackedPlayerRepository.AddAsync(tier1A);
        await trackedPlayerRepository.AddAsync(tier1B);
        await trackedPlayerRepository.AddAsync(tier1Disabled);
        await trackedPlayerRepository.AddAsync(tier2Active);

        await playerSnapshotRepository.AddAsync(new PlayerSnapshot
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = tier1A.Id,
            CurrentPp = 10000m,
            GlobalRank = 500,
            TopScoreId = 11,
            TopScorePp = 1000m,
            CapturedAt = now.AddDays(-1)
        });

        await playerSnapshotRepository.AddAsync(new PlayerSnapshot
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = tier1B.Id,
            CurrentPp = 9000m,
            GlobalRank = 700,
            TopScoreId = 22,
            TopScorePp = 800m,
            CapturedAt = now.AddDays(-15)
        });

        var stockA = CreateStock(tier1A.Id, 1200m, 15m, 20m, now.AddDays(-1));
        var stockB = CreateStock(tier1B.Id, 800m, 8m, 10m, now.AddDays(-1));

        await playerStockRepository.AddAsync(stockA);
        await playerStockRepository.AddAsync(stockB);

        var oauthService = new FakeOsuOAuthService();
        var osuApiClient = new ConfigurableOsuApiClient(
            new Dictionary<long, OsuUserProfile>
            {
                [1001] = new(1001, "mrekk", "https://avatar.example/mrekk", 10100m, 450, 12, 1010m),
                [1002] = new(1002, "whitecat", "https://avatar.example/whitecat", 9000m, 700, 22, 800m),
                [1003] = new(1003, "vaxei", "https://avatar.example/vaxei", 11000m, 300, 33, 900m),
                [1004] = new(1004, "other", "https://avatar.example/other", 7000m, 1200, 44, 600m)
            });

        var snapshotComparisonService = new SnapshotComparisonService();
        IApplicationDbContext dbContext = new NoOpApplicationDbContext();

        var service = new PlayerSynchronizationService(
            oauthService,
            osuApiClient,
            snapshotComparisonService,
            trackedPlayerRepository,
            playerSnapshotRepository,
            playerStockRepository,
            marketEventRepository,
            dbContext);

        var summary = await service.SynchronizeTrackedPlayersAsync(TrackingTier.Tier1);

        Assert.Equal(2, summary.TrackedPlayers);
        Assert.Equal(2, summary.SnapshotsCreated);
        // mrekk: PpIncreased + TopPlayDetected + RankChanged (500->450, a 10% move); whitecat: PlayerInactive.
        Assert.Equal(4, summary.EventsDetected);
        Assert.Equal(1, summary.RankImprovementsDetected);

        Assert.Equal(4, playerSnapshotRepository.AddedCountSinceStart);

        var eventTypes = marketEventRepository.Events.Select(x => x.EventType).ToList();
        Assert.Contains("PpIncreased", eventTypes);
        Assert.Contains("TopPlayDetected", eventTypes);
        Assert.Contains("RankChanged", eventTypes);
        Assert.Contains("PlayerInactive", eventTypes);
        Assert.Equal(4, eventTypes.Count);

        var updatedStockA = await playerStockRepository.GetByTrackedPlayerIdAsync(tier1A.Id);
        var updatedStockB = await playerStockRepository.GetByTrackedPlayerIdAsync(tier1B.Id);

        Assert.NotNull(updatedStockA);
        Assert.NotNull(updatedStockB);
        Assert.Equal(1200m, updatedStockA.CurrentPrice);
        Assert.Equal(15m, updatedStockA.DemandScore);
        Assert.Equal(20m, updatedStockA.PerformanceScore);
        Assert.Equal(800m, updatedStockB.CurrentPrice);
        Assert.Equal(8m, updatedStockB.DemandScore);
        Assert.Equal(10m, updatedStockB.PerformanceScore);
    }

    [Fact]
    public async Task SynchronizeTier3_WhenNoPreviousSnapshot_PersistsSnapshotWithoutEvents()
    {
        var trackedPlayerRepository = new InMemoryTrackedPlayerRepository();
        var playerSnapshotRepository = new InMemoryPlayerSnapshotRepository();
        var playerStockRepository = new InMemoryPlayerStockRepository();
        var marketEventRepository = new InMemoryMarketEventRepository();

        var tier3 = CreateTrackedPlayer(2001, "new-player", TrackingTier.Tier3, isActive: true);
        await trackedPlayerRepository.AddAsync(tier3);

        await playerStockRepository.AddAsync(CreateStock(tier3.Id, 500m, 1m, 1m, DateTimeOffset.UtcNow.AddDays(-1)));

        var service = new PlayerSynchronizationService(
            new FakeOsuOAuthService(),
            new ConfigurableOsuApiClient(new Dictionary<long, OsuUserProfile>
            {
                [2001] = new(2001, "new-player", "https://avatar.example/new-player", 4000m, 2500, 321, 500m)
            }),
            new SnapshotComparisonService(),
            trackedPlayerRepository,
            playerSnapshotRepository,
            playerStockRepository,
            marketEventRepository,
            new NoOpApplicationDbContext());

        var summary = await service.SynchronizeTrackedPlayersAsync(TrackingTier.Tier3);

        Assert.Equal(1, summary.TrackedPlayers);
        Assert.Equal(1, summary.SnapshotsCreated);
        Assert.Equal(0, summary.EventsDetected);
        Assert.Equal(0, summary.RankImprovementsDetected);
        Assert.Equal(1, playerSnapshotRepository.AddedCountSinceStart);
        Assert.Empty(marketEventRepository.Events);
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

    private static PlayerStock CreateStock(
        Guid trackedPlayerId,
        decimal currentPrice,
        decimal demandScore,
        decimal performanceScore,
        DateTimeOffset lastUpdated)
    {
        return new PlayerStock
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = trackedPlayerId,
            CurrentPrice = currentPrice,
            DemandScore = demandScore,
            PerformanceScore = performanceScore,
            CreatedAt = lastUpdated,
            LastUpdated = lastUpdated,
            CreatedBy = "test"
        };
    }

    private sealed class ConfigurableOsuApiClient(IReadOnlyDictionary<long, OsuUserProfile> users) : IOsuApiClient
    {
        public Task<OsuUserProfile> GetCurrentUserAsync(
            string accessToken,
            bool includeTopScore = true,
            CancellationToken cancellationToken = default)
        {
            var first = users.Values.FirstOrDefault()
                        ?? throw new InvalidOperationException("No users configured.");
            return Task.FromResult(first);
        }

        public Task<OsuUserProfile> GetUserAsync(
            long osuUserId,
            string accessToken,
            bool includeTopScore = true,
            CancellationToken cancellationToken = default)
        {
            if (!users.TryGetValue(osuUserId, out var user))
            {
                throw new HttpRequestException($"osu user '{osuUserId}' not configured.");
            }

            return Task.FromResult(user);
        }

        public Task<OsuTopScore?> GetTopScoreAsync(
            long osuUserId,
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            if (!users.TryGetValue(osuUserId, out var user) || user.TopScoreId is null)
            {
                return Task.FromResult<OsuTopScore?>(null);
            }

            return Task.FromResult<OsuTopScore?>(new OsuTopScore(user.TopScoreId.Value, user.TopScorePp));
        }

        public Task<IReadOnlyList<OsuTopScore>> GetTopScoresAsync(
            long osuUserId,
            string accessToken,
            int limit = 10,
            CancellationToken cancellationToken = default)
        {
            if (!users.TryGetValue(osuUserId, out var user) || user.TopScoreId is null)
            {
                return Task.FromResult<IReadOnlyList<OsuTopScore>>([]);
            }

            return Task.FromResult<IReadOnlyList<OsuTopScore>>(
                [new OsuTopScore(user.TopScoreId.Value, user.TopScorePp)]);
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

        public Task<IReadOnlyList<OsuUserProfile>> GetPerformanceRankingsAsync(
            int page,
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            var ranking = page <= 1
                ? users.Values.OrderBy(x => x.GlobalRank ?? int.MaxValue).ToList()
                : [];
            return Task.FromResult<IReadOnlyList<OsuUserProfile>>(ranking);
        }
    }

    private sealed class InMemoryPlayerSnapshotRepository : IPlayerSnapshotRepository
    {
        private readonly ConcurrentDictionary<Guid, List<PlayerSnapshot>> _snapshotsByTrackedPlayerId = new();
        public int AddedCountSinceStart { get; private set; }

        public Task AddAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            var list = _snapshotsByTrackedPlayerId.GetOrAdd(snapshot.TrackedPlayerId, _ => []);
            list.Add(Clone(snapshot));
            AddedCountSinceStart++;
            return Task.CompletedTask;
        }

        public Task<PlayerSnapshot?> GetLatestByTrackedPlayerIdAsync(
            Guid trackedPlayerId,
            CancellationToken cancellationToken = default)
        {
            if (!_snapshotsByTrackedPlayerId.TryGetValue(trackedPlayerId, out var list) || list.Count == 0)
            {
                return Task.FromResult<PlayerSnapshot?>(null);
            }

            var latest = list.OrderByDescending(x => x.CapturedAt).First();
            return Task.FromResult<PlayerSnapshot?>(Clone(latest));
        }

        public Task<IReadOnlyDictionary<Guid, PlayerSnapshot>> GetLatestByTrackedPlayerIdsAsync(
            IReadOnlyCollection<Guid> trackedPlayerIds,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<Guid, PlayerSnapshot>();

            foreach (var id in trackedPlayerIds)
            {
                if (_snapshotsByTrackedPlayerId.TryGetValue(id, out var list) && list.Count > 0)
                {
                    result[id] = Clone(list.OrderByDescending(x => x.CapturedAt).First());
                }
            }

            return Task.FromResult<IReadOnlyDictionary<Guid, PlayerSnapshot>>(result);
        }

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
        {
            var removed = 0;
            foreach (var list in _snapshotsByTrackedPlayerId.Values)
            {
                removed += list.RemoveAll(x => x.CapturedAt < cutoff);
            }

            return Task.FromResult(removed);
        }

        private static PlayerSnapshot Clone(PlayerSnapshot snapshot)
        {
            return new PlayerSnapshot
            {
                Id = snapshot.Id,
                TrackedPlayerId = snapshot.TrackedPlayerId,
                CurrentPp = snapshot.CurrentPp,
                GlobalRank = snapshot.GlobalRank,
                TopScoreId = snapshot.TopScoreId,
                TopScorePp = snapshot.TopScorePp,
                CapturedAt = snapshot.CapturedAt
            };
        }
    }

    private sealed class InMemoryPlayerStockRepository : IPlayerStockRepository
    {
        private readonly ConcurrentDictionary<Guid, PlayerStock> _stocksByTrackedPlayerId = new();

        public Task<PlayerStock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var stock = _stocksByTrackedPlayerId.Values.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(stock is null ? null : Clone(stock));
        }

        public Task<PlayerStock?> GetByTrackedPlayerIdAsync(Guid trackedPlayerId, CancellationToken cancellationToken = default)
        {
            _stocksByTrackedPlayerId.TryGetValue(trackedPlayerId, out var stock);
            return Task.FromResult(stock is null ? null : Clone(stock));
        }

        public Task<IReadOnlyList<PlayerStock>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var items = _stocksByTrackedPlayerId.Values
                .OrderByDescending(x => x.CurrentPrice)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(Clone)
                .ToList();

            return Task.FromResult<IReadOnlyList<PlayerStock>>(items);
        }

        public Task AddAsync(PlayerStock playerStock, CancellationToken cancellationToken = default)
        {
            _stocksByTrackedPlayerId[playerStock.TrackedPlayerId] = Clone(playerStock);
            return Task.CompletedTask;
        }

        public void Update(PlayerStock playerStock)
        {
            _stocksByTrackedPlayerId[playerStock.TrackedPlayerId] = Clone(playerStock);
        }

        private static PlayerStock Clone(PlayerStock stock)
        {
            return new PlayerStock
            {
                Id = stock.Id,
                TrackedPlayerId = stock.TrackedPlayerId,
                CurrentPrice = stock.CurrentPrice,
                DemandScore = stock.DemandScore,
                PerformanceScore = stock.PerformanceScore,
                CreatedAt = stock.CreatedAt,
                CreatedBy = stock.CreatedBy,
                LastUpdated = stock.LastUpdated,
                UpdatedAt = stock.UpdatedAt,
                UpdatedBy = stock.UpdatedBy
            };
        }
    }

    private sealed class InMemoryMarketEventRepository : IMarketEventRepository
    {
        private readonly List<MarketEvent> _events = [];

        public IReadOnlyList<MarketEvent> Events => _events;

        public Task AddAsync(MarketEvent marketEvent, CancellationToken cancellationToken = default)
        {
            _events.Add(Clone(marketEvent));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MarketEvent>> GetLatestByStockIdAsync(
            Guid stockId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var items = _events
                .Where(x => x.StockId == stockId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .Select(Clone)
                .ToList();

            return Task.FromResult<IReadOnlyList<MarketEvent>>(items);
        }

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
        {
            var removed = _events.RemoveAll(x => x.CreatedAt < cutoff);
            return Task.FromResult(removed);
        }

        private static MarketEvent Clone(MarketEvent marketEvent)
        {
            return new MarketEvent
            {
                Id = marketEvent.Id,
                StockId = marketEvent.StockId,
                EventType = marketEvent.EventType,
                Payload = marketEvent.Payload,
                CreatedAt = marketEvent.CreatedAt
            };
        }
    }
}
