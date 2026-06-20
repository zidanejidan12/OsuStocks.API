namespace OsuStocks.Application.Features.Market.GetStockAnalytics;

public sealed record GetStockAnalyticsResponse(
    long Volume24hShares,
    decimal Volume24hValue,
    long Volume7dShares,
    decimal Volume7dValue,
    decimal Volatility7d,
    int OwnershipCount,
    int ActiveTraders24h,
    decimal MarketCap,
    decimal Liquidity,
    string LiquidityTier,
    decimal TotalShares,
    decimal MaxOwnershipPercentage);
