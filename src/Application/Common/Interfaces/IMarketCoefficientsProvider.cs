using OsuStocks.Domain.Market.Models;

namespace OsuStocks.Application.Common.Interfaces;

public interface IMarketCoefficientsProvider
{
    Task<MarketPricingCoefficients> GetCurrentAsync(CancellationToken cancellationToken = default);
}
