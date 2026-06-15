namespace OsuStocks.Domain.Models.Market;

public sealed record MarketStockDetailsReadModel(
    Guid StockId,
    string PlayerName,
    string? AvatarUrl,
    string? CountryCode,
    decimal CurrentPrice,
    long Volume,
    decimal PriceChange24h,
    int? GlobalRank,
    decimal? CurrentPp);
