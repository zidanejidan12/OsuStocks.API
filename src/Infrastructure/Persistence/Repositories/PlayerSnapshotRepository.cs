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
}
