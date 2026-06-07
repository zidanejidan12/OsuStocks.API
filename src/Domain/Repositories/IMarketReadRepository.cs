using OsuStocks.Domain.Models.Market;

namespace OsuStocks.Domain.Repositories;

public interface IMarketReadRepository
{
    Task<MarketOverviewReadModel> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<MarketStocksPageReadModel> GetStocksAsync(MarketStocksQuerySpec spec, CancellationToken cancellationToken = default);
    Task<MarketStockDetailsReadModel?> GetStockDetailsAsync(Guid stockId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketStockHistoryPointReadModel>> GetStockHistoryAsync(Guid stockId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockCandleReadModel>> GetStockCandlesAsync(Guid stockId, HistoryRangeSpec spec, CancellationToken cancellationToken = default);
    Task<StockAnalyticsReadModel?> GetStockAnalyticsAsync(Guid stockId, CancellationToken cancellationToken = default);
}
