using OsuStocks.Domain.Market.Interfaces;
using OsuStocks.Domain.Market.Models;

namespace OsuStocks.Domain.Market.Services;

public sealed class MarketPriceEngine : IMarketPriceEngine
{
    public MarketPriceCalculation Calculate(
        decimal currentPrice,
        MarketPriceInput input,
        MarketPricingCoefficients coefficients)
    {
        var basePrice = currentPrice <= 0m ? coefficients.PriceFloor : currentPrice;

        var percentageChange = input.Type switch
        {
            MarketInputType.BuyOrderExecuted => coefficients.TradeBuyImpactPerShare * input.Quantity,
            MarketInputType.SellOrderExecuted => -coefficients.TradeSellImpactPerShare * input.Quantity,
            MarketInputType.TopPlayDetected => CalculateTopPlayImpact(input, coefficients),
            MarketInputType.PpIncreased => CalculatePpImpact(input, coefficients),
            MarketInputType.PlayerInactive => -coefficients.InactivityDecayImpact,
            _ => 0m
        };

        var rawPrice = basePrice * (1m + percentageChange);
        var newPrice = rawPrice < coefficients.PriceFloor ? coefficients.PriceFloor : rawPrice;

        return new MarketPriceCalculation(basePrice, decimal.Round(newPrice, 4), percentageChange);
    }

    private static decimal CalculateTopPlayImpact(MarketPriceInput input, MarketPricingCoefficients coefficients)
    {
        // Scale the bump by how big this play is relative to the player's overall pp: a breakout play
        // (large fraction of a smaller player's pp) moves the stock more than the same pp play from a
        // top player. Fall back to the floor when pp data is unavailable (e.g. older events).
        if (input.TopPlayPp <= 0m || input.CurrentPp <= 0m)
        {
            return coefficients.MinTopPlayImpact;
        }

        var ratio = input.TopPlayPp / input.CurrentPp;
        var impact = coefficients.TopPlayImpactScale * ratio;
        return Math.Clamp(impact, coefficients.MinTopPlayImpact, coefficients.MaxTopPlayImpact);
    }

    private static decimal CalculatePpImpact(MarketPriceInput input, MarketPricingCoefficients coefficients)
    {
        var ppDelta = input.CurrentPp - input.PreviousPp;
        if (ppDelta <= 0m)
        {
            return 0m;
        }

        var impact = ppDelta * coefficients.PpImpactPerPoint;
        return impact > coefficients.MaxPpImpact ? coefficients.MaxPpImpact : impact;
    }
}
