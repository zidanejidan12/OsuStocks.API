using OsuStocks.Domain.Models.Market;

namespace OsuStocks.Domain.Repositories;

public interface IMarketActivityReadRepository
{
    Task<IReadOnlyList<MarketActivityItemReadModel>> GetFeedAsync(
        int skip,
        int take,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MarketActivityItemReadModel>> GetFeedByStockAsync(
        Guid stockId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockTopPlayReadModel>> GetTopPlaysByStockAsync(
        Guid stockId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
