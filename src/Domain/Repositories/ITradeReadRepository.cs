using OsuStocks.Domain.Models;

namespace OsuStocks.Domain.Repositories;

public interface ITradeReadRepository
{
    Task<IReadOnlyList<TradeHistoryReadModel>> GetTradeHistoryByUserIdAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
