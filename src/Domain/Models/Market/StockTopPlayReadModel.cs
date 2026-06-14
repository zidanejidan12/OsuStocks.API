namespace OsuStocks.Domain.Models.Market;

// A recent top play that moved a stock's price: the osu! score (for linking), its pp, and the
// price impact recorded by the market engine (null when no correlated price-history row is found).
public sealed record StockTopPlayReadModel(
    Guid StockId,
    long ScoreId,
    decimal? Pp,
    string? CoverUrl,
    string? Title,
    decimal? PercentChange,
    decimal? NewPrice,
    DateTimeOffset OccurredAt);
