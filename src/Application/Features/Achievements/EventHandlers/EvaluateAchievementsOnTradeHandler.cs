using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Domain.Achievements.Interfaces;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Achievements.EventHandlers;

/// <summary>
/// Evaluates achievements after a buy or sell commits (post-commit, best-effort, mirroring the
/// investor-XP handler). Derives the user's lifetime metrics from existing data, unlocks any newly
/// satisfied achievements (idempotently), grants the credit reward, and creates an
/// <c>AchievementUnlocked</c> notification. Failures never fail the originating trade.
///
/// Level-based achievements are eventually consistent: a trade that triggers a level-up may run
/// before the XP handler in the same dispatch, so the achievement unlocks on the next trade.
/// </summary>
public sealed class EvaluateAchievementsOnTradeHandler(
    IAchievementCatalog catalog,
    IProgressionMetricsReadRepository metricsRepository,
    IUserAchievementRepository achievementRepository,
    INotificationRepository notificationRepository,
    IApplicationDbContext dbContext,
    ILogger<EvaluateAchievementsOnTradeHandler> logger)
    : INotificationHandler<BuyOrderExecutedNotification>,
      INotificationHandler<SellOrderExecutedNotification>
{
    public Task Handle(BuyOrderExecutedNotification notification, CancellationToken cancellationToken)
        => EvaluateAsync(notification.Event.UserId, notification.Event.OccurredAt, cancellationToken);

    public Task Handle(SellOrderExecutedNotification notification, CancellationToken cancellationToken)
        => EvaluateAsync(notification.Event.UserId, notification.Event.OccurredAt, cancellationToken);

    private async Task EvaluateAsync(Guid userId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        try
        {
            var unlocked = (await achievementRepository.GetUnlockedAsync(userId, cancellationToken))
                .Select(u => u.AchievementCode)
                .ToHashSet();

            var candidates = catalog.All.Where(a => !unlocked.Contains(a.Code)).ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            var metrics = await metricsRepository.GetAchievementMetricsAsync(userId, cancellationToken);

            foreach (var achievement in candidates)
            {
                if (metrics.ValueOf(achievement.Metric) < achievement.Threshold)
                {
                    continue;
                }

                var granted = await achievementRepository.TryUnlockAndRewardAsync(
                    userId, achievement.Code, achievement.RewardCredits, occurredAt, cancellationToken);

                if (granted)
                {
                    await AddUnlockNotificationAsync(userId, achievement.Code, achievement.Name,
                        achievement.RewardCredits, occurredAt, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to evaluate achievements for user {UserId}.", userId);
        }
    }

    private async Task AddUnlockNotificationAsync(
        Guid userId,
        string code,
        string name,
        long rewardCredits,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Serialize(new { code, name, rewardCredits });

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = "AchievementUnlocked",
            Title = $"Achievement unlocked: {name}",
            Body = rewardCredits > 0
                ? $"You unlocked \"{name}\" and earned {rewardCredits:N0} credits."
                : $"You unlocked \"{name}\".",
            Data = data,
            IsRead = false,
            CreatedAt = occurredAt,
        };

        await notificationRepository.AddRangeAsync(new[] { notification }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
