using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IWealthSnapshotRepository
{
    Task AddRangeAsync(IEnumerable<WealthSnapshot> snapshots, CancellationToken cancellationToken = default);

    Task<WealthSnapshot?> GetWealthAtOrBeforeAsync(
        Guid userId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WealthSnapshot>> BuildSnapshotsForAllUsersAsync(
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default);
}
