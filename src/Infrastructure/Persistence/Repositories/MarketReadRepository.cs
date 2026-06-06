using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Models.Market;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class MarketReadRepository(AppDbContext dbContext) : IMarketReadRepository
{
    public async Task<MarketOverviewReadModel> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var totalStocks = await dbContext.PlayerStocks
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var totalVolume = await dbContext.Trades
            .AsNoTracking()
            .Select(x => (long?)x.Quantity)
            .SumAsync(cancellationToken) ?? 0L;

        var rows = await BuildStockMetricRowQuery(DateTimeOffset.UtcNow.AddHours(-24))
            .ToListAsync(cancellationToken);

        var items = rows.Select(ToListItem).ToList();

        var topGainer = items
            .OrderByDescending(x => x.PriceChange24h)
            .ThenBy(x => x.PlayerName)
            .FirstOrDefault();

        var topLoser = items
            .OrderBy(x => x.PriceChange24h)
            .ThenBy(x => x.PlayerName)
            .FirstOrDefault();

        return new MarketOverviewReadModel(
            totalStocks,
            totalVolume,
            topGainer is null
                ? null
                : new MarketTopMoverReadModel(
                    topGainer.StockId, topGainer.PlayerName, topGainer.CurrentPrice, topGainer.PriceChange24h),
            topLoser is null
                ? null
                : new MarketTopMoverReadModel(
                    topLoser.StockId, topLoser.PlayerName, topLoser.CurrentPrice, topLoser.PriceChange24h));
    }

    public async Task<MarketStocksPageReadModel> GetStocksAsync(
        MarketStocksQuerySpec spec,
        CancellationToken cancellationToken = default)
    {
        var rowQuery = BuildStockMetricRowQuery(DateTimeOffset.UtcNow.AddHours(-24));

        if (!string.IsNullOrWhiteSpace(spec.Search))
        {
            var searchTerm = spec.Search.Trim().ToLower();
            rowQuery = rowQuery.Where(x => x.PlayerName.ToLower().Contains(searchTerm));
        }

        // The 24h change depends on a reference price that is a coalesce of correlated
        // subqueries and the current price; EF cannot translate that arithmetic (nor sort
        // by it) in SQL. The tracked-stock set is small, so we materialize the translatable
        // projection (filtered in SQL) and compute/sort/paginate the derived metric in memory.
        var rows = await rowQuery.ToListAsync(cancellationToken);

        var items = rows.Select(ToListItem).ToList();
        var totalCount = items.Count;

        var pageItems = ApplySorting(items, spec.Sort)
            .Skip((spec.Page - 1) * spec.PageSize)
            .Take(spec.PageSize)
            .ToList();

        return new MarketStocksPageReadModel(pageItems, totalCount);
    }

    public async Task<MarketStockDetailsReadModel?> GetStockDetailsAsync(
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        var row = await BuildStockMetricRowQuery(DateTimeOffset.UtcNow.AddHours(-24))
            .Where(x => x.StockId == stockId)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new MarketStockDetailsReadModel(
            row.StockId,
            row.PlayerName,
            row.CurrentPrice,
            row.Volume,
            ComputeChange24h(row));
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

    // Projects only SQL-translatable pieces: the player join, the trade-volume aggregate,
    // and the baseline/fallback reference prices via correlated subqueries. No arithmetic
    // or cross-subquery coalesce here, so EF can translate the whole query.
    private IQueryable<StockMetricRow> BuildStockMetricRowQuery(DateTimeOffset cutoff)
    {
        return
            from stock in dbContext.PlayerStocks.AsNoTracking()
            join player in dbContext.TrackedPlayers.AsNoTracking()
                on stock.TrackedPlayerId equals player.Id
            select new StockMetricRow
            {
                StockId = stock.Id,
                PlayerName = player.Username,
                CurrentPrice = stock.CurrentPrice,
                Volume = dbContext.Trades
                    .Where(x => x.StockId == stock.Id)
                    .Select(x => (long?)x.Quantity)
                    .Sum() ?? 0L,
                BaselinePrice = dbContext.StockPriceHistory
                    .Where(x => x.StockId == stock.Id && x.CreatedAt <= cutoff)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => (decimal?)x.NewPrice)
                    .FirstOrDefault(),
                FallbackPrice = dbContext.StockPriceHistory
                    .Where(x => x.StockId == stock.Id)
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => (decimal?)x.PreviousPrice)
                    .FirstOrDefault()
            };
    }

    private static MarketStockListItemReadModel ToListItem(StockMetricRow row)
    {
        return new MarketStockListItemReadModel(
            row.StockId,
            row.PlayerName,
            row.CurrentPrice,
            row.Volume,
            ComputeChange24h(row));
    }

    private static decimal ComputeChange24h(StockMetricRow row)
    {
        var referencePrice = row.BaselinePrice ?? row.FallbackPrice ?? row.CurrentPrice;

        return referencePrice == 0m
            ? 0m
            : ((row.CurrentPrice - referencePrice) / referencePrice) * 100m;
    }

    private static IReadOnlyList<MarketStockListItemReadModel> ApplySorting(
        IReadOnlyList<MarketStockListItemReadModel> items,
        string? sort)
    {
        return (sort?.Trim().ToLowerInvariant() switch
        {
            "price_asc" => items.OrderBy(x => x.CurrentPrice).ThenBy(x => x.PlayerName),
            "price_desc" => items.OrderByDescending(x => x.CurrentPrice).ThenBy(x => x.PlayerName),
            "name_asc" => items.OrderBy(x => x.PlayerName),
            "name_desc" => items.OrderByDescending(x => x.PlayerName),
            "volume_asc" => items.OrderBy(x => x.Volume).ThenBy(x => x.PlayerName),
            "volume_desc" => items.OrderByDescending(x => x.Volume).ThenBy(x => x.PlayerName),
            "change24h_asc" => items.OrderBy(x => x.PriceChange24h).ThenBy(x => x.PlayerName),
            "change24h_desc" => items.OrderByDescending(x => x.PriceChange24h).ThenBy(x => x.PlayerName),
            _ => items.OrderByDescending(x => x.CurrentPrice).ThenBy(x => x.PlayerName)
        }).ToList();
    }

    private sealed class StockMetricRow
    {
        public Guid StockId { get; init; }
        public string PlayerName { get; init; } = string.Empty;
        public decimal CurrentPrice { get; init; }
        public long Volume { get; init; }
        public decimal? BaselinePrice { get; init; }
        public decimal? FallbackPrice { get; init; }
    }
}
