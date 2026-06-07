namespace OsuStocks.Domain.Models.Market;

public sealed record TrendingReadModel(
    IReadOnlyList<TrendingStockReadModel> MostBought,
    IReadOnlyList<TrendingStockReadModel> MostSold,
    IReadOnlyList<TrendingStockReadModel> FastestRising,
    IReadOnlyList<TrendingStockReadModel> FastestFalling,
    IReadOnlyList<TrendingStockReadModel> HighestVolume);

public sealed record TrendingStockReadModel(
    Guid StockId,
    string PlayerName,
    string? AvatarUrl,
    string? CountryCode,
    decimal MetricValue,
    decimal CurrentPrice);
