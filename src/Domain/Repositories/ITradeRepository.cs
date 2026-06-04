using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface ITradeRepository
{
    Task AddAsync(Trade trade, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trade>> GetByUserIdAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
