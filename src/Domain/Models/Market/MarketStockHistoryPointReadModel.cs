namespace OsuStocks.Domain.Models.Market;

public sealed record MarketStockHistoryPointReadModel(
    DateTimeOffset Timestamp,
    decimal Price);
