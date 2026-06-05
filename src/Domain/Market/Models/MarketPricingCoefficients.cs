namespace OsuStocks.Domain.Market.Models;

public sealed record MarketPricingCoefficients(
    decimal TradeBuyImpactPerShare,
    decimal TradeSellImpactPerShare,
    decimal TopPlayImpact,
    decimal PpImpactPerPoint,
    decimal MaxPpImpact,
    decimal InactivityDecayImpact,
    decimal PriceFloor);
