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

    /// <summary>
    /// Returns, per player, the most recent snapshot captured at or before <paramref name="cutoff"/>
    /// — the player's state as of that instant. Players with no snapshot that old are absent from the
    /// result. Used to measure pp movement across the inactivity window.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, PlayerSnapshot>> GetLatestAtOrBeforeByTrackedPlayerIdsAsync(
        IReadOnlyCollection<Guid> trackedPlayerIds,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default);

    /// <summary>Bulk-deletes snapshots captured before <paramref name="cutoff"/>. Returns rows removed.</summary>
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}
