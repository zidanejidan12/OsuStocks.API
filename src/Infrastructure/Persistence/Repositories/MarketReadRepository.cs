using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Models.Market;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class MarketReadRepository(AppDbContext dbContext) : IMarketReadRepository
{
    public async Task<MarketOverviewReadModel> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var metricsQuery = BuildStockMetricsQuery(DateTimeOffset.UtcNow.AddHours(-24));

        var totalStocks = await dbContext.PlayerStocks
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var totalVolume = await dbContext.Trades
            .AsNoTracking()
            .Select(x => (long?)x.Quantity)
            .SumAsync(cancellationToken) ?? 0L;

        var topGainerMetric = await metricsQuery
            .OrderByDescending(x => x.PriceChange24h)
            .FirstOrDefaultAsync(cancellationToken);

        var topLoserMetric = await metricsQuery
            .OrderBy(x => x.PriceChange24h)
            .FirstOrDefaultAsync(cancellationToken);

        return new MarketOverviewReadModel(
            totalStocks,
            totalVolume,
            topGainerMetric is null
                ? null
                : new MarketTopMoverReadModel(
                    topGainerMetric.StockId,
                    topGainerMetric.PlayerName,
                    topGainerMetric.CurrentPrice,
                    topGainerMetric.PriceChange24h),
            topLoserMetric is null
                ? null
                : new MarketTopMoverReadModel(
                    topLoserMetric.StockId,
                    topLoserMetric.PlayerName,
                    topLoserMetric.CurrentPrice,
                    topLoserMetric.PriceChange24h));
    }

    public async Task<MarketStocksPageReadModel> GetStocksAsync(
        MarketStocksQuerySpec spec,
        CancellationToken cancellationToken = default)
    {
        var query = BuildStockMetricsQuery(DateTimeOffset.UtcNow.AddHours(-24));

        if (!string.IsNullOrWhiteSpace(spec.Search))
        {
            var searchTerm = spec.Search.Trim().ToLower();
            query = query.Where(x => x.PlayerName.ToLower().Contains(searchTerm));
        }

        query = ApplySorting(query, spec.Sort);

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (spec.Page - 1) * spec.PageSize;

        var items = await query
            .Skip(skip)
            .Take(spec.PageSize)
            .ToListAsync(cancellationToken);

        return new MarketStocksPageReadModel(items, totalCount);
    }

    public Task<MarketStockDetailsReadModel?> GetStockDetailsAsync(Guid stockId, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);

        var query =
            from stock in dbContext.PlayerStocks.AsNoTracking()
            join player in dbContext.TrackedPlayers.AsNoTracking()
                on stock.TrackedPlayerId equals player.Id
            where stock.Id == stockId
            let volume = dbContext.Trades
                .AsNoTracking()
                .Where(x => x.StockId == stock.Id)
                .Select(x => (long?)x.Quantity)
                .Sum() ?? 0L
            let baselinePrice = dbContext.StockPriceHistory
                .AsNoTracking()
                .Where(x => x.StockId == stock.Id && x.CreatedAt <= cutoff)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (decimal?)x.NewPrice)
                .FirstOrDefault()
            let fallbackPrice = dbContext.StockPriceHistory
                .AsNoTracking()
                .Where(x => x.StockId == stock.Id)
                .OrderBy(x => x.CreatedAt)
                .Select(x => (decimal?)x.PreviousPrice)
                .FirstOrDefault()
            let referencePrice = baselinePrice ?? fallbackPrice ?? stock.CurrentPrice
            select new MarketStockDetailsReadModel(
                stock.Id,
                player.Username,
                stock.CurrentPrice,
                volume,
                referencePrice == 0m ? 0m : ((stock.CurrentPrice - referencePrice) / referencePrice) * 100m);

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarketStockHistoryPointReadModel>> GetStockHistoryAsync(
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.StockPriceHistory
            .AsNoTracking()
            .Where(x => x.StockId == stockId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new MarketStockHistoryPointReadModel(x.CreatedAt, x.NewPrice))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<MarketStockListItemReadModel> BuildStockMetricsQuery(DateTimeOffset cutoff)
    {
        return
            from stock in dbContext.PlayerStocks.AsNoTracking()
            join player in dbContext.TrackedPlayers.AsNoTracking()
                on stock.TrackedPlayerId equals player.Id
            let volume = dbContext.Trades
                .AsNoTracking()
                .Where(x => x.StockId == stock.Id)
                .Select(x => (long?)x.Quantity)
                .Sum() ?? 0L
            let baselinePrice = dbContext.StockPriceHistory
                .AsNoTracking()
                .Where(x => x.StockId == stock.Id && x.CreatedAt <= cutoff)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (decimal?)x.NewPrice)
                .FirstOrDefault()
            let fallbackPrice = dbContext.StockPriceHistory
                .AsNoTracking()
                .Where(x => x.StockId == stock.Id)
                .OrderBy(x => x.CreatedAt)
                .Select(x => (decimal?)x.PreviousPrice)
                .FirstOrDefault()
            let referencePrice = baselinePrice ?? fallbackPrice ?? stock.CurrentPrice
            select new MarketStockListItemReadModel(
                stock.Id,
                player.Username,
                stock.CurrentPrice,
                volume,
                referencePrice == 0m ? 0m : ((stock.CurrentPrice - referencePrice) / referencePrice) * 100m);
    }

    private static IQueryable<MarketStockListItemReadModel> ApplySorting(
        IQueryable<MarketStockListItemReadModel> query,
        string? sort)
    {
        return sort?.Trim().ToLowerInvariant() switch
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
    }
}
