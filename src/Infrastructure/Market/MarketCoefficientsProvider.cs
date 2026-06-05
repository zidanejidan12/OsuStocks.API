using Microsoft.Extensions.Options;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Domain.Market.Models;
using OsuStocks.Infrastructure.Market.Options;

namespace OsuStocks.Infrastructure.Market;

internal sealed class MarketCoefficientsProvider(IOptions<MarketEngineOptions> options) : IMarketCoefficientsProvider
{
    public MarketPricingCoefficients GetCurrent()
    {
        var value = options.Value;

        return new MarketPricingCoefficients(
            value.TradeBuyImpactPerShare,
            value.TradeSellImpactPerShare,
            value.TopPlayImpact,
            value.PpImpactPerPoint,
            value.MaxPpImpact,
            value.InactivityDecayImpact,
            value.PriceFloor <= 0m ? 1m : value.PriceFloor);
    }
}
