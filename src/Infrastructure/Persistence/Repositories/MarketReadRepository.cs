using Microsoft.EntityFrameworkCore;
using Npgsql;
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
                    topGainer.StockId, topGainer.PlayerName, topGainer.CurrentPrice, topGainer.PriceChange24h, topGainer.AvatarUrl),
            topLoser is null
                ? null
                : new MarketTopMoverReadModel(
                    topLoser.StockId, topLoser.PlayerName, topLoser.CurrentPrice, topLoser.PriceChange24h, topLoser.AvatarUrl));
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

        if (!string.IsNullOrWhiteSpace(spec.Country))
        {
            var country = spec.Country.Trim().ToLower();
            rowQuery = rowQuery.Where(x => x.CountryCode != null && x.CountryCode.ToLower() == country);
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

    public async Task<IReadOnlyList<MarketCountryReadModel>> GetCountriesAsync(
        CancellationToken cancellationToken = default)
    {
        // One row per tracked stock, grouped by the player's country. Null/empty codes are excluded so
        // only real countries appear. Ordered by count desc then code asc to match the API contract.
        return await (
            from stock in dbContext.PlayerStocks.AsNoTracking()
            join player in dbContext.TrackedPlayers.AsNoTracking()
                on stock.TrackedPlayerId equals player.Id
            where player.CountryCode != null && player.CountryCode != string.Empty
            group stock by player.CountryCode into g
            orderby g.Count() descending, g.Key ascending
            select new MarketCountryReadModel(g.Key!, g.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarketStockListItemReadModel>> GetTopMoversAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        // The 24h change is computed in memory (see GetStocksAsync), so materialize the small tracked
        // set and rank by the largest absolute move — gainers and losers both — for the live ticker.
        var rows = await BuildStockMetricRowQuery(DateTimeOffset.UtcNow.AddHours(-24))
            .ToListAsync(cancellationToken);

        return rows
            .Select(ToListItem)
            .OrderByDescending(x => Math.Abs(x.PriceChange24h))
            .ThenByDescending(x => x.CurrentPrice)
            .Take(Math.Clamp(limit, 1, 50))
            .ToList();
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

        // Latest synced rank/pp for this one player — a cheap correlated lookup. We keep this
        // off the market-list query so the 5,000-row board stays fast.
        var rankInfo = await (
            from stock in dbContext.PlayerStocks.AsNoTracking()
            where stock.Id == stockId
            let latest = dbContext.PlayerSnapshots
                .Where(s => s.TrackedPlayerId == stock.TrackedPlayerId)
                .OrderByDescending(s => s.CapturedAt)
                .FirstOrDefault()
            select new
            {
                GlobalRank = latest != null ? latest.GlobalRank : null,
                CurrentPp = latest != null ? (decimal?)latest.CurrentPp : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new MarketStockDetailsReadModel(
            row.StockId,
            row.PlayerName,
            row.AvatarUrl,
            row.CountryCode,
            row.CurrentPrice,
            row.Volume,
            ComputeChange24h(row),
            rankInfo?.GlobalRank,
            rankInfo?.CurrentPp);
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

    public async Task<IReadOnlyList<StockCandleReadModel>> GetStockCandlesAsync(
        Guid stockId,
        HistoryRangeSpec spec,
        CancellationToken cancellationToken = default)
    {
        // Fixed-width time buckets aligned to the Unix epoch (UTC): each row's timestamp is floored
        // to a multiple of the bucket width since 1970. EXTRACT(EPOCH FROM timestamptz) is absolute
        // (timezone-independent), so bucket_start is stable regardless of the session TimeZone and
        // groups correctly for every width (1 minute .. 1 day). Open/Close are the first/last
        // new_price by created_at; high/low are max/min; volume is SUM(trade.quantity) per bucket.
        // @bucket is a fixed server-side interval literal (HistoryRangeSpec); stock_id/from are bound.
        const string sql = @"
WITH price_rows AS (
    SELECT
        h.new_price,
        h.created_at,
        to_timestamp(
            floor(EXTRACT(EPOCH FROM h.created_at) / EXTRACT(EPOCH FROM @bucket::interval))
            * EXTRACT(EPOCH FROM @bucket::interval)) AS bucket_start
    FROM stock_price_history h
    WHERE h.stock_id = @stock_id
      AND h.created_at >= @from
),
ohlc AS (
    SELECT
        bucket_start,
        (array_agg(new_price ORDER BY created_at ASC))[1]  AS open,
        (array_agg(new_price ORDER BY created_at DESC))[1] AS close,
        MAX(new_price) AS high,
        MIN(new_price) AS low
    FROM price_rows
    GROUP BY bucket_start
),
volumes AS (
    SELECT
        to_timestamp(
            floor(EXTRACT(EPOCH FROM t.executed_at) / EXTRACT(EPOCH FROM @bucket::interval))
            * EXTRACT(EPOCH FROM @bucket::interval)) AS bucket_start,
        SUM(t.quantity) AS volume
    FROM trades t
    WHERE t.stock_id = @stock_id
      AND t.executed_at >= @from
    GROUP BY 1
)
SELECT
    o.bucket_start                                AS ""BucketStart"",
    o.open                                        AS ""Open"",
    o.high                                        AS ""High"",
    o.low                                         AS ""Low"",
    o.close                                       AS ""Close"",
    COALESCE(v.volume, 0)::bigint                 AS ""Volume""
FROM ohlc o
LEFT JOIN volumes v ON v.bucket_start = o.bucket_start
ORDER BY o.bucket_start ASC;";

        var rows = await dbContext.Database
            .SqlQueryRaw<CandleRow>(
                sql,
                new NpgsqlParameter("stock_id", stockId),
                new NpgsqlParameter("from", spec.From.UtcDateTime),
                new NpgsqlParameter("bucket", spec.BucketInterval))
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new StockCandleReadModel(
                new DateTimeOffset(DateTime.SpecifyKind(x.BucketStart, DateTimeKind.Utc)),
                x.Open,
                x.High,
                x.Low,
                x.Close,
                x.Volume))
            .ToList();
    }

    public async Task<StockAnalyticsReadModel?> GetStockAnalyticsAsync(
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        var stock = await dbContext.PlayerStocks
            .AsNoTracking()
            .Where(x => x.Id == stockId)
            .Select(x => new { x.CurrentPrice })
            .FirstOrDefaultAsync(cancellationToken);

        if (stock is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var cutoff24h = now.AddHours(-24);
        var cutoff7d = now.AddDays(-7);

        // Uses ix_trade_stock_executed (stock_id, executed_at). Direct per-aggregate queries (no
        // GroupBy) so each translates to a simple SQL aggregate, and the distinct trader count is a
        // top-level COUNT(DISTINCT user_id) which EF translates cleanly.
        var trades24h = dbContext.Trades
            .AsNoTracking()
            .Where(x => x.StockId == stockId && x.ExecutedAt >= cutoff24h);
        var volume24hShares = await trades24h.Select(x => (long?)x.Quantity).SumAsync(cancellationToken) ?? 0L;
        var volume24hValue = await trades24h.Select(x => (decimal?)x.TotalAmount).SumAsync(cancellationToken) ?? 0m;
        var activeTraders24h = await trades24h.Select(x => x.UserId).Distinct().CountAsync(cancellationToken);

        var trades7d = dbContext.Trades
            .AsNoTracking()
            .Where(x => x.StockId == stockId && x.ExecutedAt >= cutoff7d);
        var volume7dShares = await trades7d.Select(x => (long?)x.Quantity).SumAsync(cancellationToken) ?? 0L;
        var volume7dValue = await trades7d.Select(x => (decimal?)x.TotalAmount).SumAsync(cancellationToken) ?? 0m;

        var ownershipCount = await dbContext.Holdings
            .AsNoTracking()
            .Where(x => x.StockId == stockId && x.Quantity > 0)
            .Select(x => x.PortfolioId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalHeldShares = await dbContext.Holdings
            .AsNoTracking()
            .Where(x => x.StockId == stockId && x.Quantity > 0)
            .Select(x => (long?)x.Quantity)
            .SumAsync(cancellationToken) ?? 0L;

        var volatility = await GetVolatility7dAsync(stockId, cutoff7d, cancellationToken);

        return new StockAnalyticsReadModel(
            volume24hShares,
            volume24hValue,
            volume7dShares,
            volume7dValue,
            volatility,
            ownershipCount,
            activeTraders24h,
            totalHeldShares * stock.CurrentPrice);
    }

    // Sample standard deviation of per-step simple returns over the 7d window. EF cannot translate
    // the window function needed to do this in SQL, and a stock's 7d price-history is small, so the
    // returns are computed in memory. Returns 0 when there are fewer than two usable returns.
    private async Task<decimal> GetVolatility7dAsync(
        Guid stockId,
        DateTimeOffset cutoff7d,
        CancellationToken cancellationToken)
    {
        var prices = await dbContext.StockPriceHistory
            .AsNoTracking()
            .Where(x => x.StockId == stockId && x.CreatedAt >= cutoff7d)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.NewPrice)
            .ToListAsync(cancellationToken);

        var returns = new List<decimal>();
        for (var i = 1; i < prices.Count; i++)
        {
            if (prices[i - 1] != 0m)
            {
                returns.Add((prices[i] - prices[i - 1]) / prices[i - 1]);
            }
        }

        if (returns.Count < 2)
        {
            return 0m;
        }

        var mean = returns.Average();
        var variance = returns.Sum(r => (r - mean) * (r - mean)) / (returns.Count - 1);
        return (decimal)Math.Sqrt((double)variance);
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
                AvatarUrl = player.AvatarUrl,
                CountryCode = player.CountryCode,
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
            row.AvatarUrl,
            row.CountryCode,
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
            // Volume/change ties (the whole board when there's been no trading yet) fall back to price
            // so recognizable top-ranked players surface instead of an alphabetical list of unknowns.
            "volume_asc" => items.OrderBy(x => x.Volume).ThenByDescending(x => x.CurrentPrice),
            "volume_desc" => items.OrderByDescending(x => x.Volume).ThenByDescending(x => x.CurrentPrice),
            "change24h_asc" => items.OrderBy(x => x.PriceChange24h).ThenByDescending(x => x.CurrentPrice),
            "change24h_desc" => items.OrderByDescending(x => x.PriceChange24h).ThenByDescending(x => x.CurrentPrice),
            _ => items.OrderByDescending(x => x.CurrentPrice).ThenBy(x => x.PlayerName)
        }).ToList();
    }

    private sealed class StockMetricRow
    {
        public Guid StockId { get; init; }
        public string PlayerName { get; init; } = string.Empty;
        public string? AvatarUrl { get; init; }
        public string? CountryCode { get; init; }
        public decimal CurrentPrice { get; init; }
        public long Volume { get; init; }
        public decimal? BaselinePrice { get; init; }
        public decimal? FallbackPrice { get; init; }
    }

    // Unmapped projection target for the raw OHLC SqlQueryRaw call; property names match the
    // double-quoted SELECT aliases so EF hydrates by column name.
    private sealed class CandleRow
    {
        public DateTime BucketStart { get; init; }
        public decimal Open { get; init; }
        public decimal High { get; init; }
        public decimal Low { get; init; }
        public decimal Close { get; init; }
        public long Volume { get; init; }
    }
}
