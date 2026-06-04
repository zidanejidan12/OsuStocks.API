using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IPlayerSnapshotRepository
{
    Task AddAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<PlayerSnapshot?> GetLatestByTrackedPlayerIdAsync(
        Guid trackedPlayerId,
        CancellationToken cancellationToken = default);
}
