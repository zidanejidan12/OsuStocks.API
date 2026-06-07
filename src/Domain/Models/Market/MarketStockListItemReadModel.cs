namespace OsuStocks.Domain.Models.Market;

public sealed record MarketStockListItemReadModel(
    Guid StockId,
    string PlayerName,
    string? AvatarUrl,
    string? CountryCode,
    decimal CurrentPrice,
    long Volume,
    decimal PriceChange24h);
