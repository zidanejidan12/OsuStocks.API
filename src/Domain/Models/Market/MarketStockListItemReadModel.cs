namespace OsuStocks.Domain.Models.Market;

public sealed record MarketStockListItemReadModel(
    Guid StockId,
    string PlayerName,
    decimal CurrentPrice,
    long Volume,
    decimal PriceChange24h);
