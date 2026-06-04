using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class TrackedPlayerRepository(AppDbContext dbContext) : ITrackedPlayerRepository
{
    public Task<TrackedPlayer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.TrackedPlayers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<TrackedPlayer?> GetByOsuUserIdAsync(long osuUserId, CancellationToken cancellationToken = default)
    {
        return dbContext.TrackedPlayers.AsNoTracking().FirstOrDefaultAsync(x => x.OsuUserId == osuUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<TrackedPlayer>> GetByOsuUserIdsAsync(
        IReadOnlyCollection<long> osuUserIds,
        CancellationToken cancellationToken = default)
    {
        if (osuUserIds.Count == 0)
        {
            return [];
        }

        return await dbContext.TrackedPlayers
            .AsNoTracking()
            .Where(x => osuUserIds.Contains(x.OsuUserId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrackedPlayer>> GetAllAsync(
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.TrackedPlayers.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(x => x.TrackingTier)
            .ThenBy(x => x.Username)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrackedPlayer>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.TrackedPlayers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.TrackingTier)
            .ThenBy(x => x.Username)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(TrackedPlayer trackedPlayer, CancellationToken cancellationToken = default)
    {
        return dbContext.TrackedPlayers.AddAsync(trackedPlayer, cancellationToken).AsTask();
    }

    public void Update(TrackedPlayer trackedPlayer)
    {
        dbContext.TrackedPlayers.Update(trackedPlayer);
    }
}
