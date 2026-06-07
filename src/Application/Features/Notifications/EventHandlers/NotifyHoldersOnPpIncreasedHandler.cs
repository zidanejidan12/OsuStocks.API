using System.Text.Json;
using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Notifications.EventHandlers;

public sealed class NotifyHoldersOnPpIncreasedHandler(
    IPlayerStockRepository playerStockRepository,
    ITrackedPlayerRepository trackedPlayerRepository,
    INotificationRepository notificationRepository,
    IApplicationDbContext dbContext)
    : INotificationHandler<PpIncreasedNotification>
{
    public async Task Handle(PpIncreasedNotification notification, CancellationToken cancellationToken)
    {
        var trackedPlayerId = notification.Event.TrackedPlayerId;

        var stock = await playerStockRepository.GetByTrackedPlayerIdAsync(trackedPlayerId, cancellationToken);
        if (stock is null)
        {
            return;
        }

        var trackedPlayer = await trackedPlayerRepository.GetByIdAsync(trackedPlayerId, cancellationToken);
        var playerName = trackedPlayer?.Username ?? "A tracked player";

        var holderUserIds = await notificationRepository.GetHolderUserIdsByStockIdAsync(stock.Id, cancellationToken);
        if (holderUserIds.Count == 0)
        {
            return;
        }

        var data = JsonSerializer.Serialize(notification.Event);
        var createdAt = notification.Event.OccurredAt == default
            ? DateTimeOffset.UtcNow
            : notification.Event.OccurredAt;

        var notifications = holderUserIds.Select(userId => new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = "PpIncreased",
            Title = $"{playerName} gained pp",
            Body = $"{playerName} gained pp, which may move the price of your holding.",
            Data = data,
            IsRead = false,
            CreatedAt = createdAt,
        });

        await notificationRepository.AddRangeAsync(notifications, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
