namespace OsuStocks.Application.Features.Trading.Services;

/// <summary>
/// Computes the progressive (PPh-21-style) service fee for a trade of the given value, scaled by the
/// live admin multiplier. The fee is burned on application (an inflation sink), charged on both buys
/// and sells. Returns 0 when fees are disabled (multiplier 0) or the value is non-positive.
/// </summary>
public interface ITradeFeePolicy
{
    Task<decimal> ComputeFeeAsync(decimal tradeValue, CancellationToken cancellationToken = default);
}
