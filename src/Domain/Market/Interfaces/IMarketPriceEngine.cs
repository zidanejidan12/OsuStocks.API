using OsuStocks.Domain.Market.Models;

namespace OsuStocks.Domain.Market.Interfaces;

public interface IMarketPriceEngine
{
    MarketPriceCalculation Calculate(
        decimal currentPrice,
        MarketPriceInput input,
        MarketPricingCoefficients coefficients);
}
