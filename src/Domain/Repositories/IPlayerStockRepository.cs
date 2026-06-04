using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IPlayerStockRepository
{
    Task<PlayerStock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PlayerStock?> GetByTrackedPlayerIdAsync(Guid trackedPlayerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayerStock>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(PlayerStock playerStock, CancellationToken cancellationToken = default);
    void Update(PlayerStock playerStock);
}
