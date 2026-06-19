using Microsoft.Extensions.Options;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Domain.Market.Models;
using OsuStocks.Domain.Repositories;
using OsuStocks.Infrastructure.Market.Options;

namespace OsuStocks.Infrastructure.Market;

internal sealed class MarketCoefficientsProvider(
    IOptions<MarketEngineOptions> options,
    IMarketSettingsRepository marketSettingsRepository) : IMarketCoefficientsProvider
{
    public async Task<MarketPricingCoefficients> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var value = options.Value;
        var settings = await marketSettingsRepository.GetCurrentAsync(cancellationToken);

        var ppMultiplier = settings?.PpMultiplier ?? 1m;
        var tradeMultiplier = settings?.TradeMultiplier ?? 1m;
        var decayMultiplier = settings?.DecayMultiplier ?? 1m;

        return new MarketPricingCoefficients(
            value.TradeBuyImpactPerShare * tradeMultiplier,
            value.TradeSellImpactPerShare * tradeMultiplier,
            value.TopPlayImpactScale * ppMultiplier,
            value.MaxTopPlayImpact * ppMultiplier,
            value.MinTopPlayImpact * ppMultiplier,
            value.PpImpactPerPoint * ppMultiplier,
            value.MaxPpImpact * ppMultiplier,
            value.InactivityDecayImpact * decayMultiplier,
            value.PriceFloor <= 0m ? 1m : value.PriceFloor,
            value.RankChangeImpactScale * ppMultiplier,
            value.MaxRankChangeImpact * ppMultiplier,
            value.MaxTradeImpact);
    }
}
