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

        if (!string.IsNullOrWhiteSpace(spec.Country))
        {
            var country = spec.Country.Trim();
            query = query.Where(x =>
                !string.IsNullOrEmpty(x.CountryCode)
                && string.Equals(x.CountryCode, country, StringComparison.OrdinalIgnoreCase));
        }

        query = spec.Sort?.Trim().ToLowerInvariant() switch
        {
            "price_asc" => query.OrderBy(x => x.CurrentPrice).ThenBy(x => x.PlayerName),
            "price_desc" => query.OrderByDescending(x => x.CurrentPrice).ThenBy(x => x.PlayerName),
            "name_asc" => query.OrderBy(x => x.PlayerName),
            "name_desc" => query.OrderByDescending(x => x.PlayerName),
            "volume_asc" => query.OrderBy(x => x.Volume).ThenByDescending(x => x.CurrentPrice),
            "volume_desc" => query.OrderByDescending(x => x.Volume).ThenByDescending(x => x.CurrentPrice),
            "change24h_asc" => query.OrderBy(x => x.PriceChange24h).ThenByDescending(x => x.CurrentPrice),
            "change24h_desc" => query.OrderByDescending(x => x.PriceChange24h).ThenByDescending(x => x.CurrentPrice),
            _ => query.OrderByDescending(x => x.CurrentPrice).ThenBy(x => x.PlayerName)
        };

        var totalCount = query.Count();

        var items = query
            .Skip((spec.Page - 1) * spec.PageSize)
            .Take(spec.PageSize)
            .Select(x => new MarketStockListItemReadModel(
                x.StockId,
                x.PlayerName,
                x.AvatarUrl,
                x.CountryCode,
                x.CurrentPrice,
                x.Volume,
                x.PriceChange24h))
            .ToList();

        return Task.FromResult(new MarketStocksPageReadModel(items, totalCount));
    }

    public Task<IReadOnlyList<MarketCountryReadModel>> GetCountriesAsync(CancellationToken cancellationToken = default)
    {
        var countries = _stocks
            .Where(x => !string.IsNullOrEmpty(x.CountryCode))
            .GroupBy(x => x.CountryCode!)
            .Select(g => new MarketCountryReadModel(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.CountryCode, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult<IReadOnlyList<MarketCountryReadModel>>(countries);
    }

    public Task<IReadOnlyList<MarketStockListItemReadModel>> GetTopMoversAsync(int limit, CancellationToken cancellationToken = default)
    {
        var movers = _stocks
            .OrderByDescending(x => Math.Abs(x.PriceChange24h))
            .ThenByDescending(x => x.CurrentPrice)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(x => new MarketStockListItemReadModel(
                x.StockId,
                x.PlayerName,
                x.AvatarUrl,
                x.CountryCode,
                x.CurrentPrice,
                x.Volume,
                x.PriceChange24h))
            .ToList();

        return Task.FromResult<IReadOnlyList<MarketStockListItemReadModel>>(movers);
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

    public Task<IReadOnlyList<StockCandleReadModel>> GetStockCandlesAsync(Guid stockId, HistoryRangeSpec spec, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<StockCandleReadModel>>([]);
    }

    public Task<StockAnalyticsReadModel?> GetStockAnalyticsAsync(Guid stockId, CancellationToken cancellationToken = default)
    {
        var stock = _stocks.FirstOrDefault(x => x.StockId == stockId);
        if (stock is null)
        {
            return Task.FromResult<StockAnalyticsReadModel?>(null);
        }

        return Task.FromResult<StockAnalyticsReadModel?>(
            new StockAnalyticsReadModel(0L, 0m, 0L, 0m, 0m, 0, 0, 0m));
    }

    private static MarketTopMoverReadModel ToTopMover(MarketStockDetailsReadModel stock)
    {
        return new MarketTopMoverReadModel(stock.StockId, stock.PlayerName, stock.CurrentPrice, stock.PriceChange24h);
    }
}
