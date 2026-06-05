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
            MarketInputType.TopPlayDetected => coefficients.TopPlayImpact,
            MarketInputType.PpIncreased => CalculatePpImpact(input, coefficients),
            MarketInputType.PlayerInactive => -coefficients.InactivityDecayImpact,
            _ => 0m
        };

        var rawPrice = basePrice * (1m + percentageChange);
        var newPrice = rawPrice < coefficients.PriceFloor ? coefficients.PriceFloor : rawPrice;

        return new MarketPriceCalculation(basePrice, decimal.Round(newPrice, 4), percentageChange);
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
