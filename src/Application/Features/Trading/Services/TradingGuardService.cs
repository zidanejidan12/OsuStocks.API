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

        // Position limit only applies when there is existing supply from other users
        if (totalSupply == 0)
        {
            return Result.Success();
        }

        var projectedTotal = totalSupply + requestedQuantity;
        var projectedUserHolding = currentHoldingQuantity + requestedQuantity;

        var ownershipPercentage = (decimal)projectedUserHolding / projectedTotal * 100;

        if (ownershipPercentage > maxPercentage)
        {
            logger.LogWarning(
                "Position limit exceeded. UserId={UserId}, StockId={StockId}, CurrentHolding={CurrentHolding}, RequestedQuantity={RequestedQuantity}, TotalSupply={TotalSupply}, OwnershipPercent={OwnershipPercent:F1}, MaxPercent={MaxPercent}",
                userId, stockId, currentHoldingQuantity, requestedQuantity, totalSupply, ownershipPercentage, maxPercentage);

            return Result.Failure("POSITION_LIMIT_EXCEEDED",
                $"This purchase would exceed the maximum ownership limit of {maxPercentage}% per stock.");
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
