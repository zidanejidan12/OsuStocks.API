using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Models.Market;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class MarketActivityReadRepository(AppDbContext dbContext) : IMarketActivityReadRepository
{
    public async Task<IReadOnlyList<MarketActivityItemReadModel>> GetFeedAsync(
        int skip,
        int take,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var query = BuildActivityRowQuery();

        if (!string.IsNullOrWhiteSpace(reason) && TryParseReason(reason, out var parsedReason))
        {
            query = query.Where(x => x.Reason == parsedReason);
        }

        var rows = await query
            .OrderByDescending(x => x.OccurredAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(ToReadModel).ToList();
    }

    public async Task<IReadOnlyList<MarketActivityItemReadModel>> GetFeedByStockAsync(
        Guid stockId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildActivityRowQuery()
            .Where(x => x.StockId == stockId)
            .OrderByDescending(x => x.OccurredAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(ToReadModel).ToList();
    }

    // Projects only SQL-translatable pieces: the player join and the raw history columns.
    // The percent-change arithmetic and the enum-to-description mapping are computed in
    // memory after materialization, mirroring MarketReadRepository.
    private IQueryable<ActivityRow> BuildActivityRowQuery()
    {
        return
            from history in dbContext.StockPriceHistory.AsNoTracking()
            join stock in dbContext.PlayerStocks.AsNoTracking()
                on history.StockId equals stock.Id
            join player in dbContext.TrackedPlayers.AsNoTracking()
                on stock.TrackedPlayerId equals player.Id
            select new ActivityRow
            {
                StockId = history.StockId,
                PlayerName = player.Username,
                AvatarUrl = player.AvatarUrl,
                CountryCode = player.CountryCode,
                Reason = history.Reason,
                PreviousPrice = history.PreviousPrice,
                NewPrice = history.NewPrice,
                OccurredAt = history.CreatedAt
            };
    }

    private static MarketActivityItemReadModel ToReadModel(ActivityRow row)
    {
        var percentChange = row.PreviousPrice == 0m
            ? 0m
            : Math.Round(((row.NewPrice - row.PreviousPrice) / row.PreviousPrice) * 100m, 2);

        return new MarketActivityItemReadModel(
            row.StockId,
            row.PlayerName,
            row.AvatarUrl,
            row.CountryCode,
            row.Reason.ToString(),
            ToDescription(row.Reason),
            percentChange,
            row.NewPrice,
            row.OccurredAt);
    }

    private static string ToDescription(PriceChangeReason reason)
    {
        return reason switch
        {
            PriceChangeReason.BuyPressure => "Heavy buy pressure",
            PriceChangeReason.SellPressure => "Sell pressure",
            PriceChangeReason.PPGain => "PP gain",
            PriceChangeReason.TopPlay => "Top play detected",
            PriceChangeReason.Decay => "Inactivity decay",
            PriceChangeReason.AdminAdjustment => "Admin adjustment",
            _ => reason.ToString()
        };
    }

    private static bool TryParseReason(string reason, out PriceChangeReason parsedReason)
    {
        return Enum.TryParse(reason.Trim(), ignoreCase: true, out parsedReason)
            && Enum.IsDefined(parsedReason);
    }

    private sealed class ActivityRow
    {
        public Guid StockId { get; init; }
        public string PlayerName { get; init; } = string.Empty;
        public string? AvatarUrl { get; init; }
        public string? CountryCode { get; init; }
        public PriceChangeReason Reason { get; init; }
        public decimal PreviousPrice { get; init; }
        public decimal NewPrice { get; init; }
        public DateTimeOffset OccurredAt { get; init; }
    }
}
