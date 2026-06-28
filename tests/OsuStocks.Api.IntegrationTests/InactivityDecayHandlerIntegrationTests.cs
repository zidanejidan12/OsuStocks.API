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
    public async Task Handle_PlayersWithNoPpGainOverWindow_PublishesDecayEvents()
    {
        var now = DateTimeOffset.UtcNow;

        var trackedPlayerRepository = new InMemoryTrackedPlayerRepository();
        var snapshotRepository = new InMemoryPlayerSnapshotRepositoryBatch();
        var publisher = new CapturingPublisher();

        // Every player has a FRESH latest snapshot (like prod, where sync writes one each cycle),
        // so recency alone never signals inactivity — only pp movement over the window does.
        var gainedPp = CreateTrackedPlayer(1001, "gained-pp");
        var flatPp = CreateTrackedPlayer(1002, "flat-pp");
        var declinedPp = CreateTrackedPlayer(1003, "declined-pp");
        var newPlayer = CreateTrackedPlayer(1004, "new-player");

        await trackedPlayerRepository.AddAsync(gainedPp);
        await trackedPlayerRepository.AddAsync(flatPp);
        await trackedPlayerRepository.AddAsync(declinedPp);
        await trackedPlayerRepository.AddAsync(newPlayer);

        // Gained pp over the 7d window (5000 -> 5300) — active, should NOT decay.
        await snapshotRepository.AddAsync(CreateSnapshot(gainedPp.Id, now.AddDays(-8), pp: 5000m));
        await snapshotRepository.AddAsync(CreateSnapshot(gainedPp.Id, now.AddHours(-1), pp: 5300m));

        // Flat pp across the window — should decay.
        await snapshotRepository.AddAsync(CreateSnapshot(flatPp.Id, now.AddDays(-8), pp: 5000m));
        await snapshotRepository.AddAsync(CreateSnapshot(flatPp.Id, now.AddHours(-1), pp: 5000m));

        // pp declined (osu! recalc) — still no gain, should decay.
        await snapshotRepository.AddAsync(CreateSnapshot(declinedPp.Id, now.AddDays(-10), pp: 5000m));
        await snapshotRepository.AddAsync(CreateSnapshot(declinedPp.Id, now.AddHours(-1), pp: 4900m));

        // Tracked for less than the window (no baseline at/before the cutoff) — cannot judge, skip.
        await snapshotRepository.AddAsync(CreateSnapshot(newPlayer.Id, now.AddDays(-2), pp: 5000m));

        var handler = new EvaluateInactivityDecayCommandHandler(
            trackedPlayerRepository,
            snapshotRepository,
            publisher,
            new StubInactivityDecaySettings(7),
            new NoOpApplicationDbContext());

        var result = await handler.Handle(new EvaluateInactivityDecayCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.PlayersEvaluated);
        Assert.Equal(2, result.Value.DecayEventsPublished);
        Assert.Equal(2, publisher.PlayerInactiveNotifications.Count);

        var decayedPlayerIds = publisher.PlayerInactiveNotifications
            .Select(n => n.Event.TrackedPlayerId)
            .ToList();

        Assert.Contains(flatPp.Id, decayedPlayerIds);
        Assert.Contains(declinedPp.Id, decayedPlayerIds);
        Assert.DoesNotContain(gainedPp.Id, decayedPlayerIds);
        Assert.DoesNotContain(newPlayer.Id, decayedPlayerIds);
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
            new StubInactivityDecaySettings(7),
            new NoOpApplicationDbContext());

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
            new StubInactivityDecaySettings(7),
            new NoOpApplicationDbContext());

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

        // Fresh latest snapshot, but pp has been flat since 4 days ago.
        await snapshotRepository.AddAsync(CreateSnapshot(player.Id, now.AddDays(-4), pp: 5000m));
        await snapshotRepository.AddAsync(CreateSnapshot(player.Id, now.AddHours(-1), pp: 5000m));

        // threshold = 3: a baseline exists at/before now-3d (the -4d snapshot) and pp is flat → decay
        var handler = new EvaluateInactivityDecayCommandHandler(
            trackedPlayerRepository,
            snapshotRepository,
            publisher,
            new StubInactivityDecaySettings(3),
            new NoOpApplicationDbContext());

        var result = await handler.Handle(new EvaluateInactivityDecayCommand(), CancellationToken.None);

        Assert.Equal(1, result.Value!.DecayEventsPublished);

        // With threshold = 7 days, this player should NOT decay
        var publisher2 = new CapturingPublisher();

        var handler2 = new EvaluateInactivityDecayCommandHandler(
            trackedPlayerRepository,
            snapshotRepository,
            publisher2,
            new StubInactivityDecaySettings(7),
            new NoOpApplicationDbContext());

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

    private static PlayerSnapshot CreateSnapshot(Guid trackedPlayerId, DateTimeOffset capturedAt, decimal pp = 5000m)
    {
        return new PlayerSnapshot
        {
            Id = Guid.NewGuid(),
            TrackedPlayerId = trackedPlayerId,
            CurrentPp = pp,
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

        public Task<IReadOnlyDictionary<Guid, PlayerSnapshot>> GetLatestAtOrBeforeByTrackedPlayerIdsAsync(
            IReadOnlyCollection<Guid> trackedPlayerIds,
            DateTimeOffset cutoff,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<Guid, PlayerSnapshot>();

            foreach (var id in trackedPlayerIds)
            {
                if (_snapshotsByPlayerId.TryGetValue(id, out var list))
                {
                    var snapshot = list
                        .Where(x => x.CapturedAt <= cutoff)
                        .OrderByDescending(x => x.CapturedAt)
                        .FirstOrDefault();
                    if (snapshot is not null)
                    {
                        result[id] = snapshot;
                    }
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
