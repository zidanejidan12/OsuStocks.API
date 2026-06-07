namespace OsuStocks.Application.Features.Market.GetTrending;

public sealed record GetTrendingResponse(
    IReadOnlyList<TrendingStockResponse> MostBought,
    IReadOnlyList<TrendingStockResponse> MostSold,
    IReadOnlyList<TrendingStockResponse> FastestRising,
    IReadOnlyList<TrendingStockResponse> FastestFalling,
    IReadOnlyList<TrendingStockResponse> HighestVolume);

public sealed record TrendingStockResponse(
    Guid StockId,
    string PlayerName,
    string? AvatarUrl,
    string? CountryCode,
    decimal MetricValue,
    decimal CurrentPrice);
