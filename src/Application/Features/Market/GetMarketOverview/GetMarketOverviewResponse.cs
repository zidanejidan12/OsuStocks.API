namespace OsuStocks.Application.Features.Market.GetMarketOverview;

public sealed record GetMarketOverviewResponse(
    int TotalStocks,
    long TotalVolume,
    MarketTopMoverResponse? TopGainer,
    MarketTopMoverResponse? TopLoser);

public sealed record MarketTopMoverResponse(
    Guid StockId,
    string PlayerName,
    decimal CurrentPrice,
    decimal PriceChange24h,
    string? AvatarUrl = null);
