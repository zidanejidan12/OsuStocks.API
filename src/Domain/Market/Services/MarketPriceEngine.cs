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
            // Trade impact is capped per order (both directions) so a single large buy/sell can't moon
            // or crash a stock — closing the pump-and-dump vector. Other drivers clamp inside their helpers.
            MarketInputType.BuyOrderExecuted =>
                Math.Clamp(coefficients.TradeBuyImpactPerShare * input.Quantity, 0m, coefficients.MaxTradeImpact),
            MarketInputType.SellOrderExecuted =>
                Math.Clamp(-coefficients.TradeSellImpactPerShare * input.Quantity, -coefficients.MaxTradeImpact, 0m),
            MarketInputType.TopPlayDetected => CalculateTopPlayImpact(input, coefficients),
            MarketInputType.PpIncreased => CalculatePpImpact(input, coefficients),
            MarketInputType.RankChanged => CalculateRankChangeImpact(input, coefficients),
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
        // Symmetric: pp gains lift the price, pp losses lower it, capped both directions.
        var ppDelta = input.CurrentPp - input.PreviousPp;
        if (ppDelta == 0m)
        {
            return 0m;
        }

        var impact = ppDelta * coefficients.PpImpactPerPoint;
        return Math.Clamp(impact, -coefficients.MaxPpImpact, coefficients.MaxPpImpact);
    }

    private static decimal CalculateRankChangeImpact(MarketPriceInput input, MarketPricingCoefficients coefficients)
    {
        // Rank is zero-sum and bidirectional. Scale by the *relative* move so a proportional change
        // matters equally at any level (rank 50->40 == rank 5000->4000 == +20%). Improving (rank
        // number falls) is positive; dropping is negative. Clamped both directions.
        if (input.PreviousRank <= 0 || input.CurrentRank <= 0 || input.PreviousRank == input.CurrentRank)
        {
            return 0m;
        }

        var delta = input.PreviousRank - input.CurrentRank; // positive = rank improved
        var relative = (decimal)delta / input.PreviousRank;
        var impact = coefficients.RankChangeImpactScale * relative;
        return Math.Clamp(impact, -coefficients.MaxRankChangeImpact, coefficients.MaxRankChangeImpact);
    }
}
