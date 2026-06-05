using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryTrackedPlayerRepository : ITrackedPlayerRepository
{
    private readonly ConcurrentDictionary<Guid, TrackedPlayer> _trackedPlayers = new();

    public Task<TrackedPlayer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _trackedPlayers.TryGetValue(id, out var trackedPlayer);
        return Task.FromResult(Clone(trackedPlayer));
    }

    public Task<TrackedPlayer?> GetByOsuUserIdAsync(long osuUserId, CancellationToken cancellationToken = default)
    {
        var trackedPlayer = _trackedPlayers.Values.FirstOrDefault(x => x.OsuUserId == osuUserId);
        return Task.FromResult(Clone(trackedPlayer));
    }

    public Task<IReadOnlyList<TrackedPlayer>> GetByOsuUserIdsAsync(
        IReadOnlyCollection<long> osuUserIds,
        CancellationToken cancellationToken = default)
    {
        var items = _trackedPlayers.Values
            .Where(x => osuUserIds.Contains(x.OsuUserId))
            .OrderBy(x => x.TrackingTier)
            .ThenBy(x => x.Username)
            .Select(Clone)
            .Cast<TrackedPlayer>()
            .ToList();

        return Task.FromResult<IReadOnlyList<TrackedPlayer>>(items);
    }

    public Task<IReadOnlyList<TrackedPlayer>> GetAllAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _trackedPlayers.Values.AsEnumerable();

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var items = query
            .OrderBy(x => x.TrackingTier)
            .ThenBy(x => x.Username)
            .Select(Clone)
            .Cast<TrackedPlayer>()
            .ToList();

        return Task.FromResult<IReadOnlyList<TrackedPlayer>>(items);
    }

    public Task<IReadOnlyList<TrackedPlayer>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var items = _trackedPlayers.Values
            .Where(x => x.IsActive)
            .OrderBy(x => x.TrackingTier)
            .ThenBy(x => x.Username)
            .Select(Clone)
            .Cast<TrackedPlayer>()
            .ToList();

        return Task.FromResult<IReadOnlyList<TrackedPlayer>>(items);
    }

    public Task<IReadOnlyList<TrackedPlayer>> GetActiveByTierAsync(
        TrackingTier tier,
        CancellationToken cancellationToken = default)
    {
        var items = _trackedPlayers.Values
            .Where(x => x.IsActive && x.TrackingTier == tier)
            .OrderBy(x => x.Username)
            .Select(Clone)
            .Cast<TrackedPlayer>()
            .ToList();

        return Task.FromResult<IReadOnlyList<TrackedPlayer>>(items);
    }

    public Task AddAsync(TrackedPlayer trackedPlayer, CancellationToken cancellationToken = default)
    {
        _trackedPlayers[trackedPlayer.Id] = Clone(trackedPlayer)!;
        return Task.CompletedTask;
    }

    public void Update(TrackedPlayer trackedPlayer)
    {
        _trackedPlayers[trackedPlayer.Id] = Clone(trackedPlayer)!;
    }

    private static TrackedPlayer? Clone(TrackedPlayer? trackedPlayer)
    {
        if (trackedPlayer is null)
        {
            return null;
        }

        return new TrackedPlayer
        {
            Id = trackedPlayer.Id,
            OsuUserId = trackedPlayer.OsuUserId,
            Username = trackedPlayer.Username,
            TrackingTier = trackedPlayer.TrackingTier,
            IsActive = trackedPlayer.IsActive,
            CreatedAt = trackedPlayer.CreatedAt,
            CreatedBy = trackedPlayer.CreatedBy,
            UpdatedAt = trackedPlayer.UpdatedAt,
            UpdatedBy = trackedPlayer.UpdatedBy
        };
    }
}
