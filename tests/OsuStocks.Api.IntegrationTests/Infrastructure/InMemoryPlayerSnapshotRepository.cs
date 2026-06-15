using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryPlayerSnapshotRepository : IPlayerSnapshotRepository
{
    private readonly ConcurrentDictionary<Guid, List<PlayerSnapshot>> _snapshotsByTrackedPlayerId = new();

    public Task AddAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var list = _snapshotsByTrackedPlayerId.GetOrAdd(snapshot.TrackedPlayerId, _ => []);
        lock (list)
        {
            list.Add(Clone(snapshot));
        }

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

        lock (list)
        {
            var latest = list.OrderByDescending(x => x.CapturedAt).First();
            return Task.FromResult<PlayerSnapshot?>(Clone(latest));
        }
    }

    public Task<IReadOnlyDictionary<Guid, PlayerSnapshot>> GetLatestByTrackedPlayerIdsAsync(
        IReadOnlyCollection<Guid> trackedPlayerIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, PlayerSnapshot>();

        foreach (var id in trackedPlayerIds)
        {
            if (_snapshotsByTrackedPlayerId.TryGetValue(id, out var list))
            {
                lock (list)
                {
                    if (list.Count > 0)
                    {
                        result[id] = Clone(list.OrderByDescending(x => x.CapturedAt).First());
                    }
                }
            }
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, PlayerSnapshot>>(result);
    }

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        var removed = 0;
        foreach (var list in _snapshotsByTrackedPlayerId.Values)
        {
            lock (list)
            {
                removed += list.RemoveAll(x => x.CapturedAt < cutoff);
            }
        }

        return Task.FromResult(removed);
    }

    public int CountFor(Guid trackedPlayerId)
    {
        if (!_snapshotsByTrackedPlayerId.TryGetValue(trackedPlayerId, out var list))
        {
            return 0;
        }

        lock (list)
        {
            return list.Count;
        }
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
