using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IPortfolioRepository
{
    Task<Portfolio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Portfolio?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Portfolio portfolio, CancellationToken cancellationToken = default);
    void Update(Portfolio portfolio);
}
