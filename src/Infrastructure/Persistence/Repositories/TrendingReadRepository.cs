using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Models.Market;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class TrendingReadRepository(AppDbContext dbContext) : ITrendingReadRepository
{
    public async Task<TrendingReadModel> GetTrendingAsync(
        DateTimeOffset windowStart,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var mostBought = await GetTradeQuantityLeadersAsync(TradeType.Buy, windowStart, limit, cancellationToken);
        var mostSold = await GetTradeQuantityLeadersAsync(TradeType.Sell, windowStart, limit, cancellationToken);
        var highestVolume = await GetHighestVolumeAsync(windowStart, limit, cancellationToken);
        var (fastestRising, fastestFalling) = await GetPriceMoversAsync(windowStart, limit, cancellationToken);

        return new TrendingReadModel(
            mostBought,
            mostSold,
            fastestRising,
            fastestFalling,
            highestVolume);
    }

    // SUM(quantity) of trades of the given type in the window, grouped by stock, top N. Uses
    // ix_trade_stock_executed (stock_id, executed_at). Joins player_stocks -> tracked_players for
    // the display name; current_price is read from the stock for the response shape.
    private async Task<IReadOnlyList<TrendingStockReadModel>> GetTradeQuantityLeadersAsync(
        TradeType tradeType,
        DateTimeOffset windowStart,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from trade in dbContext.Trades.AsNoTracking()
            where trade.TradeType == tradeType && trade.ExecutedAt >= windowStart
            group trade by trade.StockId into g
            orderby g.Sum(x => (long)x.Quantity) descending
            select new { StockId = g.Key, Metric = g.Sum(x => (long)x.Quantity) })
            .Take(limit)
            .Join(
                dbContext.PlayerStocks.AsNoTracking(),
                x => x.StockId,
                stock => stock.Id,
                (x, stock) => new { x.Metric, stock.Id, stock.CurrentPrice, stock.TrackedPlayerId })
            .Join(
                dbContext.TrackedPlayers.AsNoTracking(),
                x => x.TrackedPlayerId,
                player => player.Id,
                (x, player) => new TrendingStockReadModel(x.Id, player.Username, player.AvatarUrl, player.CountryCode, x.Metric, x.CurrentPrice))
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(x => x.MetricValue)
            .ThenBy(x => x.PlayerName)
            .ToList();
    }

    // SUM(total_amount) of all trades in the window, grouped by stock, top N. Uses
    // ix_trade_stock_executed (stock_id, executed_at).
    private async Task<IReadOnlyList<TrendingStockReadModel>> GetHighestVolumeAsync(
        DateTimeOffset windowStart,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from trade in dbContext.Trades.AsNoTracking()
            where trade.ExecutedAt >= windowStart
            group trade by trade.StockId into g
            orderby g.Sum(x => x.TotalAmount) descending
            select new { StockId = g.Key, Metric = g.Sum(x => x.TotalAmount) })
            .Take(limit)
            .Join(
                dbContext.PlayerStocks.AsNoTracking(),
                x => x.StockId,
                stock => stock.Id,
                (x, stock) => new { x.Metric, stock.Id, stock.CurrentPrice, stock.TrackedPlayerId })
            .Join(
                dbContext.TrackedPlayers.AsNoTracking(),
                x => x.TrackedPlayerId,
                player => player.Id,
                (x, player) => new TrendingStockReadModel(x.Id, player.Username, player.AvatarUrl, player.CountryCode, x.Metric, x.CurrentPrice))
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(x => x.MetricValue)
            .ThenBy(x => x.PlayerName)
            .ToList();
    }

    // Percent change over the window = (current_price - reference_price) / reference_price * 100,
    // where reference_price is the latest history price at-or-before windowStart (falling back to the
    // earliest known previous price, then current price), reusing the baseline-subquery approach from
    // MarketReadRepository.BuildStockMetricRowQuery. The reference is a cross-subquery coalesce that EF
    // cannot translate (nor sort by), and the tracked-stock set is small, so the change is computed and
    // ranked in memory. Returns (rising desc, falling asc).
    private async Task<(IReadOnlyList<TrendingStockReadModel> Rising, IReadOnlyList<TrendingStockReadModel> Falling)>
        GetPriceMoversAsync(
            DateTimeOffset windowStart,
            int limit,
            CancellationToken cancellationToken)
    {
        var rows = await (
            from stock in dbContext.PlayerStocks.AsNoTracking()
            join player in dbContext.TrackedPlayers.AsNoTracking()
                on stock.TrackedPlayerId equals player.Id
            select new PriceMoverRow
            {
                StockId = stock.Id,
                PlayerName = player.Username,
                AvatarUrl = player.AvatarUrl,
                CountryCode = player.CountryCode,
                CurrentPrice = stock.CurrentPrice,
                BaselinePrice = dbContext.StockPriceHistory
                    .Where(x => x.StockId == stock.Id && x.CreatedAt <= windowStart)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => (decimal?)x.NewPrice)
                    .FirstOrDefault(),
                FallbackPrice = dbContext.StockPriceHistory
                    .Where(x => x.StockId == stock.Id)
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => (decimal?)x.PreviousPrice)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var movers = rows
            .Select(row => new TrendingStockReadModel(
                row.StockId,
                row.PlayerName,
                row.AvatarUrl,
                row.CountryCode,
                ComputePercentChange(row),
                row.CurrentPrice))
            .ToList();

        var rising = movers
            .OrderByDescending(x => x.MetricValue)
            .ThenBy(x => x.PlayerName)
            .Take(limit)
            .ToList();

        var falling = movers
            .OrderBy(x => x.MetricValue)
            .ThenBy(x => x.PlayerName)
            .Take(limit)
            .ToList();

        return (rising, falling);
    }

    private static decimal ComputePercentChange(PriceMoverRow row)
    {
        var referencePrice = row.BaselinePrice ?? row.FallbackPrice ?? row.CurrentPrice;

        return referencePrice == 0m
            ? 0m
            : ((row.CurrentPrice - referencePrice) / referencePrice) * 100m;
    }

    private sealed class PriceMoverRow
    {
        public Guid StockId { get; init; }
        public string PlayerName { get; init; } = string.Empty;
        public string? AvatarUrl { get; init; }
        public string? CountryCode { get; init; }
        public decimal CurrentPrice { get; init; }
        public decimal? BaselinePrice { get; init; }
        public decimal? FallbackPrice { get; init; }
    }
}
