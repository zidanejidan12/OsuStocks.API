using System.Text.Json;
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

    public async Task<IReadOnlyList<StockTopPlayReadModel>> GetTopPlaysByStockAsync(
        Guid stockId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Recent top-play events carry the score id + pp in their JSON payload. The matching price
        // impact lives in stock_price_history (Reason = TopPlay); both rows are written with the same
        // sync-cycle timestamp, so they correlate exactly on (stock_id, created_at).
        var events = await dbContext.MarketEvents
            .AsNoTracking()
            .Where(e => e.StockId == stockId && e.EventType == "TopPlayDetected")
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(e => new { e.StockId, e.CreatedAt, e.Payload })
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return [];
        }

        var timestamps = events.Select(e => e.CreatedAt).Distinct().ToList();
        var priceRows = await dbContext.StockPriceHistory
            .AsNoTracking()
            .Where(h => h.StockId == stockId
                && h.Reason == PriceChangeReason.TopPlay
                && timestamps.Contains(h.CreatedAt))
            .Select(h => new { h.CreatedAt, h.PreviousPrice, h.NewPrice })
            .ToListAsync(cancellationToken);

        // Multiple plays can share one sync-cycle timestamp, so a timestamp maps to several price
        // rows. Parse payloads up front, then pair each play to its OWN row: within a timestamp, the
        // k-th play by pp <-> the k-th row by chain order (PreviousPrice asc). Impacts compound
        // multiplicatively (order-invariant), so this pairing is purely cosmetic but ensures each
        // play shows its own impact instead of all sharing one row's value.
        var priceRowsByTime = priceRows
            .GroupBy(p => p.CreatedAt)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.PreviousPrice).ToList());

        var parsed = events
            .Select(e => new { e.StockId, e.CreatedAt, Payload = ParseTopPlayPayload(e.Payload) })
            .ToList();

        var priceByEvent = new Dictionary<int, (decimal Prev, decimal New)>();
        foreach (var group in parsed
            .Select((e, index) => (e, index))
            .GroupBy(x => x.e.CreatedAt))
        {
            if (!priceRowsByTime.TryGetValue(group.Key, out var rows))
            {
                continue;
            }

            var playsByPp = group.OrderBy(x => x.e.Payload?.NewTopScorePp ?? 0m).ToList();
            for (var i = 0; i < playsByPp.Count && i < rows.Count; i++)
            {
                priceByEvent[playsByPp[i].index] = (rows[i].PreviousPrice, rows[i].NewPrice);
            }
        }

        var result = new List<StockTopPlayReadModel>(parsed.Count);
        for (var index = 0; index < parsed.Count; index++)
        {
            var marketEvent = parsed[index];

            decimal? percentChange = null;
            decimal? newPrice = null;
            if (priceByEvent.TryGetValue(index, out var price))
            {
                newPrice = price.New;
                percentChange = price.Prev == 0m
                    ? 0m
                    : Math.Round(((price.New - price.Prev) / price.Prev) * 100m, 2);
            }

            result.Add(new StockTopPlayReadModel(
                marketEvent.StockId,
                marketEvent.Payload?.NewTopScoreId ?? 0,
                marketEvent.Payload?.NewTopScorePp,
                marketEvent.Payload?.CoverUrl,
                marketEvent.Payload?.Title,
                percentChange,
                newPrice,
                marketEvent.CreatedAt));
        }

        return result;
    }

    private static TopPlayPayload? ParseTopPlayPayload(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<TopPlayPayload>(payload, TopPlayPayloadOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions TopPlayPayloadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record TopPlayPayload
    {
        public long NewTopScoreId { get; init; }
        public decimal? NewTopScorePp { get; init; }
        public string? CoverUrl { get; init; }
        public string? Title { get; init; }
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
