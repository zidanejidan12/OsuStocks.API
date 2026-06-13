using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Investor.Interfaces;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Investor.EventHandlers;

/// <summary>
/// Awards investor XP from trading volume after a buy or sell commits (post-commit, best-effort,
/// mirroring the holder fan-out notification handlers). XP earned = floor of gross traded credits
/// (UnitPrice * Quantity), applied to both buys and sells. When the award advances the user's
/// level, an in-app "level up" notification is created.
///
/// XP is a cosmetic side effect of an already-committed trade, so failures here must never fail the
/// originating request: the whole award is wrapped and any error is logged and swallowed.
/// </summary>
public sealed class AwardInvestorXpOnTradeHandler(
    IInvestorProfileRepository investorProfileRepository,
    IInvestorLevelCalculator levelCalculator,
    INotificationRepository notificationRepository,
    IApplicationDbContext dbContext,
    ILogger<AwardInvestorXpOnTradeHandler> logger)
    : INotificationHandler<BuyOrderExecutedNotification>,
      INotificationHandler<SellOrderExecutedNotification>
{
    public Task Handle(BuyOrderExecutedNotification notification, CancellationToken cancellationToken)
    {
        var e = notification.Event;
        return AwardAsync(e.UserId, e.UnitPrice * e.Quantity, e.OccurredAt, cancellationToken);
    }

    public Task Handle(SellOrderExecutedNotification notification, CancellationToken cancellationToken)
    {
        var e = notification.Event;
        return AwardAsync(e.UserId, e.UnitPrice * e.Quantity, e.OccurredAt, cancellationToken);
    }

    private async Task AwardAsync(
        Guid userId,
        decimal tradedAmount,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var xpGain = (long)decimal.Floor(tradedAmount);
        if (xpGain <= 0L)
        {
            return;
        }

        try
        {
            var result = await investorProfileRepository.AddXpAsync(userId, xpGain, occurredAt, cancellationToken);

            if (result.LeveledUp)
            {
                await AddLevelUpNotificationAsync(userId, result.NewLevel, occurredAt, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: never let a cosmetic XP award fail an already-committed trade.
            logger.LogWarning(ex, "Failed to award investor XP for user {UserId}.", userId);
        }
    }

    private async Task AddLevelUpNotificationAsync(
        Guid userId,
        int level,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var title = levelCalculator.GetTitle(level);
        var data = JsonSerializer.Serialize(new { level, title });

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = "InvestorLevelUp",
            Title = $"You reached investor level {level}",
            Body = $"You're now a {title} (level {level}). Keep trading to climb the ranks.",
            Data = data,
            IsRead = false,
            CreatedAt = occurredAt,
        };

        await notificationRepository.AddRangeAsync(new[] { notification }, cancellationToken);
    }
}
