using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface ITrackedPlayerRepository
{
    Task<TrackedPlayer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TrackedPlayer?> GetByOsuUserIdAsync(long osuUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackedPlayer>> GetByOsuUserIdsAsync(IReadOnlyCollection<long> osuUserIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackedPlayer>> GetAllAsync(bool? isActive = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackedPlayer>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackedPlayer>> GetActiveByTierAsync(
        OsuStocks.Domain.Common.Enums.TrackingTier tier,
        CancellationToken cancellationToken = default);
    Task AddAsync(TrackedPlayer trackedPlayer, CancellationToken cancellationToken = default);
    void Update(TrackedPlayer trackedPlayer);
    void Remove(TrackedPlayer trackedPlayer);
}
