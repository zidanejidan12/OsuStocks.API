using System.Text.Json;
using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Notifications.EventHandlers;

public sealed class NotifyHoldersOnTopPlayDetectedHandler(
    IPlayerStockRepository playerStockRepository,
    ITrackedPlayerRepository trackedPlayerRepository,
    INotificationRepository notificationRepository,
    IApplicationDbContext dbContext)
    : INotificationHandler<TopPlayDetectedNotification>
{
    public async Task Handle(TopPlayDetectedNotification notification, CancellationToken cancellationToken)
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

        // Include the stock id so the client can deep-link the notification to the stock page.
        var data = JsonSerializer.Serialize(new { stockId = stock.Id });
        var createdAt = notification.Event.OccurredAt == default
            ? DateTimeOffset.UtcNow
            : notification.Event.OccurredAt;

        var notifications = holderUserIds.Select(userId => new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = "TopPlayDetected",
            Title = $"{playerName} set a new top play",
            Body = $"{playerName} set a new top play, which may move the price of your holding.",
            Data = data,
            IsRead = false,
            CreatedAt = createdAt,
        });

        await notificationRepository.AddRangeAsync(notifications, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
