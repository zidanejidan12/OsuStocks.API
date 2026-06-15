using MediatR;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Application.Features.OsuIntegration.InactivityDecay;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class InactivityDecayHandlerIntegrationTests
{
    [Fact]
    public async Task Handle_PlayersInactiveForMoreThanThreshold_PublishesDecayEvents()
    {
        var now = DateTimeOffset.UtcNow;

        var trackedPlayerRepository = new InMemoryTrackedPlayerRepository();
        var snapshotRepository = new InMemoryPlayerSnapshotRepositoryBatch();
        var publisher = new CapturingPublisher();

        var activeRecent = CreateTrackedPlayer(1001, "recent-player");
        var activeStale = CreateTrackedPlayer(1002, "stale-player");
        var activeVeryStale = CreateTrackedPlayer(1003, "very-stale-player");

        await trackedPlayerRepository.AddAsync(activeRecent);
        await trackedPlayerRepository.AddAsync(activeStale);
        await trackedPlayerRepository.AddAsync(activeVeryStale);

        // Recent snapshot (2 days ago) — should NOT decay
        await snapshotRepository.AddAsync(CreateSnapshot(activeRecent.Id, now.AddDays(-2)));

        // Stale snapshot (8 days ago) — should decay (threshold = 7)
        await snapshotRepository.AddAsync(CreateSnapshot(activeStale.Id, now.AddDays(-8)));

        // Very stale snapshot (30 days ago) — should decay
        await snapshotRepository.AddAsync(CreateSnapshot(activeVeryStale.Id, now.AddDays(-30)));

        var handler = new EvaluateInactivityDecayCommandHandler(
            trackedPlayerRepository,
            snapshotRepository,
            publisher,
            new StubInactivityDecaySettings(7));

        var result = await handler.Handle(new EvaluateInactivityDecayCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.PlayersEvaluated);
        Assert.Equal(2, result.Value.DecayEventsPublished);
        Assert.Equal(2, publisher.PlayerInactiveNotifications.Count);

        var decayedPlayerIds = publisher.PlayerInactiveNotifications
            .Select(n => n.Event.TrackedPlayerId)
            .ToList();

        Assert.Contains(activeStale.Id, decayedPlayerIds);
        Assert.Contains(activeVeryStale.Id, decayedPlayerIds);
        Assert.DoesNotContain(activeRecent.Id, decayedPlayerIds);
    }

    [Fact]
    public async Task Handle_NoActivePlayers_ReturnsZeroCounts()
    {
        var trackedPlayerRepository = new InMemoryTrackedPlayerRepository();
        var snapshotRepository = new InMemoryPlayerSnapshotRepositoryBatch();
        var publisher = new CapturingPublisher();
        var handler = new EvaluateInactivityDecayCommandHandler(
            trackedPlayerRepository,
            snapshotRepository,
            publisher,
            new StubInactivityDecaySettings(7));

        var result = await handler.Handle(new EvaluateInactivityDecayCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.PlayersEvaluated);
        Assert.Equal(0, result.Value.DecayEventsPublished);
    }

    [Fact]
    public async Task Handle_PlayerWithNoSnapshot_IsSkipped()
    {
        var trackedPlayerRepository = new InMemoryTrackedPlayerRepository();
        var snapshotRepository = new InMemoryPlayerSnapshotRepositoryBatch();
        var publisher = new CapturingPublisher();

        var playerNoSnapshot = CreateTrackedPlayer(1001, "no-snapshot");
        await trackedPlayerRepository.AddAsync(playerNoSnapshot);

        var handler = new EvaluateInactivityDecayCommandHandler(
            trackedPlayerRepository,
            snapshotRepository,
            publisher,
            new StubInactivityDecaySettings(7));

        var result = await handler.Handle(new EvaluateInactivityDecayCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.PlayersEvaluated);
        Assert.Equal(0, result.Value.DecayEventsPublished);
        Assert.Empty(publisher.PlayerInactiveNotifications);
    }

    [Fact]
    public async Task Handle_CustomThreshold_UsesConfiguredValue()
    {
        var now = DateTimeOffset.UtcNow;

        var trackedPlayerRepository = new InMemoryTrackedPlayerRepository();
        var snapshotRepository = new InMemoryPlayerSnapshotRepositoryBatch();
        var publisher = new CapturingPublisher();

        var player = CreateTrackedPlayer(1001, "threshold-test");
        await trackedPlayerRepository.AddAsync(player);

        // Snapshot 4 days ago
        await snapshotRepository.AddAsync(CreateSnapshot(player.Id, now.AddDays(-4)));

        // With threshold = 3 days, this player should decay
        var handler = new EvaluateInactivityDecayCommandHandler(
            trackedPlayerRepository,
            snapshotRepository,
            publisher,
            new StubInactivityDecaySettings(3));

        var result = await handler.Handle(new EvaluateInactivityDecayCommand(), CancellationToken.None);

        Assert.Equal(1, result.Value!.DecayEventsPublished);

        // With threshold = 7 days, this player should NOT decay
        var publisher2 = new CapturingPublisher();

        var handler2 = new EvaluateInactivityDecayCommandHandler(
            trackedPlayerRepository,
            snapshotRepository,
            publisher2,
            new StubInactivityDecaySettings(7));

        var result2 = await handler2.Handle(new EvaluateInactivityDecayCommand(), CancellationToken.None);

        Assert.Equal(0, result2.Value!.DecayEventsPublished);
    }

    private static TrackedPlayer CreateTrackedPlayer(long osuUserId, string username)
    {
        return new TrackedPlayer
        {
            Id = Guid.NewGuid(),
            OsuUserId = osuUserId,
            Username = username,
            TrackingTier = TrackingTier.Tier1,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test"
        };
    }

    private static PlayerSnapshot CreateSnapshot(Guid trackedPlayerId, DateTimeOffset capturedAt)
    {
        return new PlayerSnapshot
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = trackedPlayerId,
            CurrentPp = 5000m,
            GlobalRank = 1000,
            TopScoreId = 1,
            TopScorePp = 500m,
            CapturedAt = capturedAt
        };
    }

    private sealed class InMemoryPlayerSnapshotRepositoryBatch : IPlayerSnapshotRepository
    {
        private readonly ConcurrentDictionary<Guid, List<PlayerSnapshot>> _snapshotsByPlayerId = new();

        public Task AddAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            var list = _snapshotsByPlayerId.GetOrAdd(snapshot.TrackedPlayerId, _ => []);
            list.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task<PlayerSnapshot?> GetLatestByTrackedPlayerIdAsync(
            Guid trackedPlayerId,
            CancellationToken cancellationToken = default)
        {
            if (!_snapshotsByPlayerId.TryGetValue(trackedPlayerId, out var list) || list.Count == 0)
            {
                return Task.FromResult<PlayerSnapshot?>(null);
            }

            return Task.FromResult<PlayerSnapshot?>(list.OrderByDescending(x => x.CapturedAt).First());
        }

        public Task<IReadOnlyDictionary<Guid, PlayerSnapshot>> GetLatestByTrackedPlayerIdsAsync(
            IReadOnlyCollection<Guid> trackedPlayerIds,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<Guid, PlayerSnapshot>();

            foreach (var id in trackedPlayerIds)
            {
                if (_snapshotsByPlayerId.TryGetValue(id, out var list) && list.Count > 0)
                {
                    result[id] = list.OrderByDescending(x => x.CapturedAt).First();
                }
            }

            return Task.FromResult<IReadOnlyDictionary<Guid, PlayerSnapshot>>(result);
        }

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
        {
            var removed = 0;
            foreach (var list in _snapshotsByPlayerId.Values)
            {
                removed += list.RemoveAll(x => x.CapturedAt < cutoff);
            }

            return Task.FromResult(removed);
        }
    }

    private sealed class StubInactivityDecaySettings(int thresholdDays) : IInactivityDecaySettings
    {
        public int InactivityThresholdDays => thresholdDays;
    }

    private sealed class CapturingPublisher : IPublisher
    {
        public List<PlayerInactiveNotification> PlayerInactiveNotifications { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            if (notification is PlayerInactiveNotification inactive)
            {
                PlayerInactiveNotifications.Add(inactive);
            }

            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            if (notification is PlayerInactiveNotification inactive)
            {
                PlayerInactiveNotifications.Add(inactive);
            }

            return Task.CompletedTask;
        }
    }
}
