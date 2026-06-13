using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Missions.Interfaces;
using OsuStocks.Domain.Missions.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Missions.EventHandlers;

/// <summary>
/// Evaluates daily and weekly missions after a buy or sell commits (post-commit, best-effort).
/// Resolves each cadence's period window from the trade's <c>OccurredAt</c>, derives the user's
/// in-period metrics from committed trades, completes any newly satisfied missions (idempotently),
/// grants the credit reward, and creates a <c>MissionCompleted</c> notification. Failures never
/// fail the originating trade.
/// </summary>
public sealed class EvaluateMissionsOnTradeHandler(
    IMissionCatalog catalog,
    IMissionPeriodCalculator periodCalculator,
    IProgressionMetricsReadRepository metricsRepository,
    IUserMissionCompletionRepository completionRepository,
    INotificationRepository notificationRepository,
    IApplicationDbContext dbContext,
    ILogger<EvaluateMissionsOnTradeHandler> logger)
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
            // Resolve the period window for each cadence present in the catalog, keyed off the trade's
            // instant so the just-committed trade is counted in the correct period.
            var periods = catalog.All
                .Select(m => m.Period)
                .Distinct()
                .ToDictionary(type => type, type => periodCalculator.GetPeriod(type, occurredAt));

            var periodKeys = periods.Values.Select(p => p.Key).ToList();
            var completed = (await completionRepository.GetCompletionsAsync(userId, periodKeys, cancellationToken))
                .Select(c => (c.MissionCode, c.PeriodKey))
                .ToHashSet();

            // Cache per-period metric snapshots so each window is queried at most once.
            var metricsByPeriod = new Dictionary<MissionPeriodType, MissionMetricsSnapshot>();

            foreach (var mission in catalog.All)
            {
                var period = periods[mission.Period];
                if (completed.Contains((mission.Code, period.Key)))
                {
                    continue;
                }

                if (!metricsByPeriod.TryGetValue(mission.Period, out var metrics))
                {
                    metrics = await metricsRepository.GetMissionMetricsAsync(
                        userId, period.Start, period.End, cancellationToken);
                    metricsByPeriod[mission.Period] = metrics;
                }

                if (metrics.ValueOf(mission.Metric) < mission.Target)
                {
                    continue;
                }

                var granted = await completionRepository.TryCompleteAndRewardAsync(
                    userId, mission.Code, period.Key, mission.RewardCredits, occurredAt, cancellationToken);

                if (granted)
                {
                    await AddCompletionNotificationAsync(userId, mission.Code, mission.Name,
                        mission.RewardCredits, occurredAt, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to evaluate missions for user {UserId}.", userId);
        }
    }

    private async Task AddCompletionNotificationAsync(
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
            Type = "MissionCompleted",
            Title = $"Mission complete: {name}",
            Body = rewardCredits > 0
                ? $"You completed \"{name}\" and earned {rewardCredits:N0} credits."
                : $"You completed \"{name}\".",
            Data = data,
            IsRead = false,
            CreatedAt = occurredAt,
        };

        await notificationRepository.AddRangeAsync(new[] { notification }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
