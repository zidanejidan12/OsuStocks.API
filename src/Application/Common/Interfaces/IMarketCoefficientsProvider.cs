using OsuStocks.Domain.Market.Models;

namespace OsuStocks.Application.Common.Interfaces;

public interface IMarketCoefficientsProvider
{
    MarketPricingCoefficients GetCurrent();
}
