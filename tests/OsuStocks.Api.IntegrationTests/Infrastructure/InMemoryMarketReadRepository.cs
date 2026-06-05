using OsuStocks.Domain.Models.Market;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryMarketReadRepository : IMarketReadRepository
{
    private readonly List<MarketStockDetailsReadModel> _stocks = [];
    private readonly Dictionary<Guid, List<MarketStockHistoryPointReadModel>> _historyByStockId = new();

    public void UpsertStock(MarketStockDetailsReadModel stock)
    {
        var index = _stocks.FindIndex(x => x.StockId == stock.StockId);
        if (index >= 0)
        {
            _stocks[index] = stock;
            return;
        }

        _stocks.Add(stock);
    }

    public void SetHistory(Guid stockId, IReadOnlyList<MarketStockHistoryPointReadModel> history)
    {
        _historyByStockId[stockId] = history.ToList();
    }

    public Task<MarketOverviewReadModel> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var totalStocks = _stocks.Count;
        var totalVolume = _stocks.Sum(x => x.Volume);

        var topGainer = _stocks
            .OrderByDescending(x => x.PriceChange24h)
            .Select(ToTopMover)
            .FirstOrDefault();

        var topLoser = _stocks
            .OrderBy(x => x.PriceChange24h)
            .Select(ToTopMover)
            .FirstOrDefault();

        return Task.FromResult(new MarketOverviewReadModel(totalStocks, totalVolume, topGainer, topLoser));
    }

    public Task<MarketStocksPageReadModel> GetStocksAsync(MarketStocksQuerySpec spec, CancellationToken cancellationToken = default)
    {
        IEnumerable<MarketStockDetailsReadModel> query = _stocks;

        if (!string.IsNullOrWhiteSpace(spec.Search))
        {
            var term = spec.Search.Trim();
            query = query.Where(x => x.PlayerName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = spec.Sort?.Trim().ToLowerInvariant() switch
        {
            "price_asc" => query.OrderBy(x => x.CurrentPrice).ThenBy(x => x.PlayerName),
            "price_desc" => query.OrderByDescending(x => x.CurrentPrice).ThenBy(x => x.PlayerName),
            "name_asc" => query.OrderBy(x => x.PlayerName),
            "name_desc" => query.OrderByDescending(x => x.PlayerName),
            "volume_asc" => query.OrderBy(x => x.Volume).ThenBy(x => x.PlayerName),
            "volume_desc" => query.OrderByDescending(x => x.Volume).ThenBy(x => x.PlayerName),
            "change24h_asc" => query.OrderBy(x => x.PriceChange24h).ThenBy(x => x.PlayerName),
            "change24h_desc" => query.OrderByDescending(x => x.PriceChange24h).ThenBy(x => x.PlayerName),
            _ => query.OrderByDescending(x => x.CurrentPrice).ThenBy(x => x.PlayerName)
        };

        var totalCount = query.Count();

        var items = query
            .Skip((spec.Page - 1) * spec.PageSize)
            .Take(spec.PageSize)
            .Select(x => new MarketStockListItemReadModel(
                x.StockId,
                x.PlayerName,
                x.CurrentPrice,
                x.Volume,
                x.PriceChange24h))
            .ToList();

        return Task.FromResult(new MarketStocksPageReadModel(items, totalCount));
    }

    public Task<MarketStockDetailsReadModel?> GetStockDetailsAsync(Guid stockId, CancellationToken cancellationToken = default)
    {
        var stock = _stocks.FirstOrDefault(x => x.StockId == stockId);
        return Task.FromResult(stock);
    }

    public Task<IReadOnlyList<MarketStockHistoryPointReadModel>> GetStockHistoryAsync(Guid stockId, CancellationToken cancellationToken = default)
    {
        if (!_historyByStockId.TryGetValue(stockId, out var history))
        {
            return Task.FromResult<IReadOnlyList<MarketStockHistoryPointReadModel>>([]);
        }

        return Task.FromResult<IReadOnlyList<MarketStockHistoryPointReadModel>>(history.OrderBy(x => x.Timestamp).ToList());
    }

    private static MarketTopMoverReadModel ToTopMover(MarketStockDetailsReadModel stock)
    {
        return new MarketTopMoverReadModel(stock.StockId, stock.PlayerName, stock.CurrentPrice, stock.PriceChange24h);
    }
}
