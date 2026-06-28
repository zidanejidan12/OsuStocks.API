using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class PlayerSnapshotRepository(AppDbContext dbContext) : IPlayerSnapshotRepository
{
    public Task AddAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        return dbContext.PlayerSnapshots.AddAsync(snapshot, cancellationToken).AsTask();
    }

    public Task<PlayerSnapshot?> GetLatestByTrackedPlayerIdAsync(
        Guid trackedPlayerId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PlayerSnapshots
            .AsNoTracking()
            .Where(x => x.TrackedPlayerId == trackedPlayerId)
            .OrderByDescending(x => x.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, PlayerSnapshot>> GetLatestByTrackedPlayerIdsAsync(
        IReadOnlyCollection<Guid> trackedPlayerIds,
        CancellationToken cancellationToken = default)
    {
        var snapshots = await dbContext.PlayerSnapshots
            .AsNoTracking()
            .Where(x => trackedPlayerIds.Contains(x.TrackedPlayerId))
            .GroupBy(x => x.TrackedPlayerId)
            .Select(g => g.OrderByDescending(x => x.CapturedAt).First())
            .ToDictionaryAsync(x => x.TrackedPlayerId, cancellationToken);

        return snapshots;
    }

    public async Task<IReadOnlyDictionary<Guid, PlayerSnapshot>> GetLatestAtOrBeforeByTrackedPlayerIdsAsync(
        IReadOnlyCollection<Guid> trackedPlayerIds,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        var snapshots = await dbContext.PlayerSnapshots
            .AsNoTracking()
            .Where(x => trackedPlayerIds.Contains(x.TrackedPlayerId) && x.CapturedAt <= cutoff)
            .GroupBy(x => x.TrackedPlayerId)
            .Select(g => g.OrderByDescending(x => x.CapturedAt).First())
            .ToDictionaryAsync(x => x.TrackedPlayerId, cancellationToken);

        return snapshots;
    }

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        // Set-based delete (no entity loading). Only the latest snapshot per player matters for sync,
        // and players sync at least hourly, so anything older than the cutoff is safe to drop.
        return dbContext.PlayerSnapshots
            .Where(x => x.CapturedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
