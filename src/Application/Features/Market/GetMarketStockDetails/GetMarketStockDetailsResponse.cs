namespace OsuStocks.Application.Features.Market.GetMarketStockDetails;

public sealed record GetMarketStockDetailsResponse(
    Guid StockId,
    string PlayerName,
    string? AvatarUrl,
    string? CountryCode,
    decimal CurrentPrice,
    long Volume,
    decimal PriceChange24h,
    int? GlobalRank,
    decimal? CurrentPp,
    string? BannerUrl);
