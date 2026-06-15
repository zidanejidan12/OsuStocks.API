using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IPlayerSnapshotRepository
{
    Task AddAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<PlayerSnapshot?> GetLatestByTrackedPlayerIdAsync(
        Guid trackedPlayerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, PlayerSnapshot>> GetLatestByTrackedPlayerIdsAsync(
        IReadOnlyCollection<Guid> trackedPlayerIds,
        CancellationToken cancellationToken = default);

    /// <summary>Bulk-deletes snapshots captured before <paramref name="cutoff"/>. Returns rows removed.</summary>
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}
