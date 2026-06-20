using Microsoft.Extensions.Options;
using OsuStocks.Application.Features.Trading.Services;
using OsuStocks.Domain.Market.Services;
using OsuStocks.Domain.Repositories;
using OsuStocks.Infrastructure.Market.Options;

namespace OsuStocks.Infrastructure.Market;

internal sealed class LiquidityProvider(
    IHoldingRepository holdingRepository,
    ITradeReadRepository tradeReadRepository,
    IOptions<MarketEngineOptions> options) : ILiquidityProvider
{
    public async Task<decimal> GetLiquidityAsync(Guid stockId, CancellationToken cancellationToken = default)
    {
        var sharesOutstanding = await holdingRepository.GetTotalQuantityByStockAsync(stockId, cancellationToken);
        var recentVolume = await tradeReadRepository.GetSharesTradedSinceAsync(
            stockId, DateTimeOffset.UtcNow.AddHours(-24), cancellationToken);

        return LiquidityCalculator.Liquidity(sharesOutstanding, recentVolume, options.Value.LiquidityVolumeWeight);
    }
}
