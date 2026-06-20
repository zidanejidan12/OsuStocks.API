namespace OsuStocks.Domain.Market.Models;

public sealed record MarketPricingCoefficients(
    decimal TradeBuyImpactPerShare,
    decimal TradeSellImpactPerShare,
    decimal TopPlayImpactScale,
    decimal MaxTopPlayImpact,
    decimal MinTopPlayImpact,
    decimal PpImpactPerPoint,
    decimal MaxPpImpact,
    decimal InactivityDecayImpact,
    decimal PriceFloor,
    decimal RankChangeImpactScale,
    decimal MaxRankChangeImpact,
    decimal MaxTradeImpact,
    // Liquidity model: trade impact and spread scale by ReferenceLiquidityDepth / (liquidity + depth).
    // SpreadBaseRate is the spread on a zero-liquidity stock; SpreadMinRate the floor for deep stocks.
    decimal ReferenceLiquidityDepth,
    decimal SpreadBaseRate,
    decimal SpreadMinRate);
