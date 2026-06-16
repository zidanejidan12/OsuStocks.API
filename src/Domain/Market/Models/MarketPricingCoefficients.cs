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
    decimal MaxRankChangeImpact);
