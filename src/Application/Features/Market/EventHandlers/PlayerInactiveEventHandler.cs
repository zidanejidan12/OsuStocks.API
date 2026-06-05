using MediatR;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Application.Features.Market.Services;
using OsuStocks.Domain.Market.Models;

namespace OsuStocks.Application.Features.Market.EventHandlers;

public sealed class PlayerInactiveEventHandler(
    IMarketEventProcessingService processingService,
    IPublisher publisher)
    : INotificationHandler<PlayerInactiveNotification>
{
    public async Task Handle(PlayerInactiveNotification notification, CancellationToken cancellationToken)
    {
        var priceChanged = await processingService.ApplyForTrackedPlayerAsync(
            notification.Event.TrackedPlayerId,
            MarketPriceInput.Inactivity(),
            notification.Event.OccurredAt,
            cancellationToken);

        if (priceChanged is not null)
        {
            await publisher.Publish(new PriceChangedNotification(priceChanged), cancellationToken);
        }
    }
}
