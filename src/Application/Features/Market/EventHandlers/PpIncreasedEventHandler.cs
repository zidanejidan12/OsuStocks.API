using MediatR;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Application.Features.Market.Services;
using OsuStocks.Domain.Market.Models;

namespace OsuStocks.Application.Features.Market.EventHandlers;

public sealed class PpIncreasedEventHandler(
    IMarketEventProcessingService processingService,
    IPublisher publisher)
    : INotificationHandler<PpIncreasedNotification>
{
    public async Task Handle(PpIncreasedNotification notification, CancellationToken cancellationToken)
    {
        var priceChanged = await processingService.ApplyForTrackedPlayerAsync(
            notification.Event.TrackedPlayerId,
            MarketPriceInput.PpIncrease(notification.Event.PreviousPp, notification.Event.CurrentPp),
            notification.Event.OccurredAt,
            cancellationToken);

        if (priceChanged is not null)
        {
            await publisher.Publish(new PriceChangedNotification(priceChanged), cancellationToken);
        }
    }
}
