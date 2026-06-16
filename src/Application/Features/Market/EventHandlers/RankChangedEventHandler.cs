using MediatR;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Application.Features.Market.Services;
using OsuStocks.Domain.Market.Models;

namespace OsuStocks.Application.Features.Market.EventHandlers;

public sealed class RankChangedEventHandler(
    IMarketEventProcessingService processingService,
    IPublisher publisher)
    : INotificationHandler<RankChangedNotification>
{
    public async Task Handle(RankChangedNotification notification, CancellationToken cancellationToken)
    {
        var priceChanged = await processingService.ApplyForTrackedPlayerAsync(
            notification.Event.TrackedPlayerId,
            MarketPriceInput.RankChange(notification.Event.PreviousRank, notification.Event.CurrentRank),
            notification.Event.OccurredAt,
            cancellationToken);

        if (priceChanged is not null)
        {
            await publisher.Publish(new PriceChangedNotification(priceChanged), cancellationToken);
        }
    }
}
