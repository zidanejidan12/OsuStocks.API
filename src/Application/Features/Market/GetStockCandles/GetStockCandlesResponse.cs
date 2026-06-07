namespace OsuStocks.Application.Features.Market.GetStockCandles;

public sealed record GetStockCandlesResponse(
    string Range,
    IReadOnlyList<StockCandleResponse> Items);

public sealed record StockCandleResponse(
    DateTimeOffset BucketStart,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);
