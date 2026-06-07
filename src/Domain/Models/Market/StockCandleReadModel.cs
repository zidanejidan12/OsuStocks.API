namespace OsuStocks.Domain.Models.Market;

public sealed record StockCandleReadModel(
    DateTimeOffset BucketStart,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);
