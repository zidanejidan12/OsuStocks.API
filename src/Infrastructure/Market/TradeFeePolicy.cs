using Microsoft.Extensions.Options;
using OsuStocks.Application.Features.Trading.Services;
using OsuStocks.Domain.Market.Models;
using OsuStocks.Domain.Market.Services;
using OsuStocks.Domain.Repositories;
using OsuStocks.Infrastructure.Market.Options;

namespace OsuStocks.Infrastructure.Market;

internal sealed class TradeFeePolicy(
    IOptions<MarketEngineOptions> options,
    IMarketSettingsRepository marketSettingsRepository) : ITradeFeePolicy
{
    public async Task<decimal> ComputeFeeAsync(decimal tradeValue, CancellationToken cancellationToken = default)
    {
        var brackets = options.Value.TradeFeeBrackets
            .Select(static b => new TradeFeeBracket(b.UpTo, b.Rate))
            .ToList();

        // Live magnitude knob from Market Settings (admin-tunable without a redeploy); default 1x.
        var settings = await marketSettingsRepository.GetCurrentAsync(cancellationToken);
        var multiplier = settings?.TradeFeeMultiplier ?? 1m;

        return TradeFeeCalculator.Compute(tradeValue, brackets, multiplier);
    }
}
