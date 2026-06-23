using Microsoft.Extensions.Logging;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Trading.Services;

public sealed class TradingGuardService(
    ITradeRepository tradeRepository,
    IHoldingRepository holdingRepository,
    IAntiAbuseSettings antiAbuseSettings,
    ILogger<TradingGuardService> logger)
    : ITradingGuardService
{
    public async Task<Result> CheckCooldownAsync(
        Guid userId,
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        var cooldownSeconds = antiAbuseSettings.TradeCooldownSeconds;
        if (cooldownSeconds <= 0)
        {
            return Result.Success();
        }

        var lastTrade = await tradeRepository.GetLastByUserAndStockAsync(userId, stockId, cancellationToken);
        if (lastTrade is null)
        {
            return Result.Success();
        }

        var elapsed = DateTimeOffset.UtcNow - lastTrade.ExecutedAt;
        if (elapsed.TotalSeconds < cooldownSeconds)
        {
            var remainingSeconds = (int)Math.Ceiling(cooldownSeconds - elapsed.TotalSeconds);

            logger.LogWarning(
                "Trade cooldown violation. UserId={UserId}, StockId={StockId}, LastTradeAt={LastTradeAt}, ElapsedSeconds={ElapsedSeconds}, CooldownSeconds={CooldownSeconds}",
                userId, stockId, lastTrade.ExecutedAt, (int)elapsed.TotalSeconds, cooldownSeconds);

            return Result.Failure("TRADE_COOLDOWN",
                $"Please wait {remainingSeconds} seconds before trading this stock again.");
        }

        return Result.Success();
    }

    public async Task<Result> CheckPositionLimitAsync(
        Guid userId,
        Guid stockId,
        decimal requestedQuantity,
        decimal currentHoldingQuantity,
        CancellationToken cancellationToken = default)
    {
        var maxPercentage = antiAbuseSettings.MaxOwnershipPercentage;
        if (maxPercentage <= 0 || maxPercentage >= 100)
        {
            return Result.Success();
        }

        var totalSupply = await holdingRepository.GetTotalQuantityByStockAsync(stockId, cancellationToken);

        // The cap is enforced against the real float PLUS a virtual reference supply. This removes
        // the "gatekeeping" deadlock on thin stocks: when the real float is tiny, MaxOwnershipPercentage
        // of it is near zero, so without the reference supply the first buyer could hold ~100% and lock
        // everyone else out. With it, every trader (including the first) gets a meaningful allowance on a
        // new stock, and the cap tapers to the true percentage once the float grows past the reference.
        // No first-buyer bypass is needed — the reference supply keeps the first buy from reaching 100%.
        var referenceSupply = Math.Max(0m, antiAbuseSettings.ReferenceSupplyShares);
        var effectiveSupply = totalSupply + referenceSupply;

        var projectedTotal = effectiveSupply + requestedQuantity;
        var projectedUserHolding = currentHoldingQuantity + requestedQuantity;

        var ownershipPercentage = (decimal)projectedUserHolding / projectedTotal * 100;

        if (ownershipPercentage > maxPercentage)
        {
            logger.LogWarning(
                "Position limit exceeded. UserId={UserId}, StockId={StockId}, CurrentHolding={CurrentHolding}, RequestedQuantity={RequestedQuantity}, TotalSupply={TotalSupply}, ReferenceSupply={ReferenceSupply}, OwnershipPercent={OwnershipPercent:F1}, MaxPercent={MaxPercent}",
                userId, stockId, currentHoldingQuantity, requestedQuantity, totalSupply, referenceSupply, ownershipPercentage, maxPercentage);

            // Largest extra quantity q that keeps (current + q) / (effectiveSupply + q) <= p, where p = maxPercentage/100.
            // Shares trade to 2 dp, so floor — the suggested amount must never itself trip the limit.
            var fraction = maxPercentage / 100m;
            var maxAdditionalRaw = (fraction * effectiveSupply - currentHoldingQuantity) / (1m - fraction);
            var maxAdditional = maxAdditionalRaw <= 0m
                ? 0m
                : Math.Floor(maxAdditionalRaw * 100m) / 100m;

            var message = maxAdditional <= 0m
                ? $"You already hold the maximum {maxPercentage:0.##}% of this stock, so you can't buy more right now."
                : $"A single trader can hold at most {maxPercentage:0.##}% of a stock — you can buy up to {maxAdditional:0.##} more share{(maxAdditional == 1m ? "" : "s")} of this one.";

            return Result.Failure("POSITION_LIMIT_EXCEEDED", message);
        }

        return Result.Success();
    }

    public async Task CheckRapidTradingAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var windowSeconds = antiAbuseSettings.RapidTradeWindowSeconds;
        var threshold = antiAbuseSettings.RapidTradeThreshold;

        if (windowSeconds <= 0 || threshold <= 0)
        {
            return;
        }

        var since = DateTimeOffset.UtcNow.AddSeconds(-windowSeconds);
        var recentCount = await tradeRepository.CountRecentByUserAsync(userId, since, cancellationToken);

        if (recentCount >= threshold)
        {
            logger.LogWarning(
                "Rapid trading detected. UserId={UserId}, TradeCount={TradeCount}, WindowSeconds={WindowSeconds}, Threshold={Threshold}",
                userId, recentCount, windowSeconds, threshold);
        }
    }
}
