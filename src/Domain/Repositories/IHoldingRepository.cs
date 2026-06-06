using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IHoldingRepository
{
    Task<Holding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Holding?> GetByPortfolioAndStockAsync(
        Guid portfolioId,
        Guid stockId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Holding>> GetByPortfolioIdAsync(Guid portfolioId, CancellationToken cancellationToken = default);
    Task<int> GetTotalQuantityByStockAsync(Guid stockId, CancellationToken cancellationToken = default);
    Task AddAsync(Holding holding, CancellationToken cancellationToken = default);
    void Update(Holding holding);
    void Remove(Holding holding);
}
