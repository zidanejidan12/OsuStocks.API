namespace OsuStocks.Application.Features.Market.GetStockTopPlays;

public sealed record GetStockTopPlaysResponse(
    IReadOnlyList<StockTopPlayItemResponse> Items);

public sealed record StockTopPlayItemResponse(
    long ScoreId,
    decimal? Pp,
    string? CoverUrl,
    string? Title,
    decimal? PercentChange,
    decimal? NewPrice,
    DateTimeOffset OccurredAt);
