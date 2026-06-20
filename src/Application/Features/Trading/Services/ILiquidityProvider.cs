namespace OsuStocks.Application.Features.Trading.Services;

/// <summary>
/// Computes a stock's current liquidity (float + weighted recent volume) used to dampen trade price
/// impact and the bid/ask spread. Higher liquidity = a deeper market that absorbs orders more.
/// </summary>
public interface ILiquidityProvider
{
    Task<decimal> GetLiquidityAsync(Guid stockId, CancellationToken cancellationToken = default);
}
