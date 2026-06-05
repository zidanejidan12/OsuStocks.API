namespace OsuStocks.Domain.Models.Market;

public sealed record MarketTopMoverReadModel(
    Guid StockId,
    string PlayerName,
    decimal CurrentPrice,
    decimal PriceChange24h);
