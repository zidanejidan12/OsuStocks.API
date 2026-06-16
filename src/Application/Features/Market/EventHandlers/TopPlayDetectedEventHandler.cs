using MediatR;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Application.Features.Market.Services;
using OsuStocks.Domain.Market.Models;

namespace OsuStocks.Application.Features.Market.EventHandlers;

public sealed class TopPlayDetectedEventHandler(
    IMarketEventProcessingService processingService,
    IPublisher publisher)
    : INotificationHandler<TopPlayDetectedNotification>
{
    public async Task Handle(TopPlayDetectedNotification notification, CancellationToken cancellationToken)
    {
        var topPlay = notification.Event;
        var priceChanged = await processingService.ApplyForTrackedPlayerAsync(
            topPlay.TrackedPlayerId,
            MarketPriceInput.TopPlay(topPlay.NewTopScorePp ?? 0m, topPlay.PlayerCurrentPp ?? 0m),
            topPlay.OccurredAt,
            cancellationToken);

        if (priceChanged is not null)
        {
            await publisher.Publish(new PriceChangedNotification(priceChanged), cancellationToken);
        }
    }
}
