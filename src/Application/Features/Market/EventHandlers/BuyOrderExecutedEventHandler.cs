using MediatR;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Application.Features.Market.Services;
using OsuStocks.Domain.Market.Models;

namespace OsuStocks.Application.Features.Market.EventHandlers;

public sealed class BuyOrderExecutedEventHandler(
    IMarketEventProcessingService processingService,
    IPublisher publisher)
    : INotificationHandler<BuyOrderExecutedNotification>
{
    public async Task Handle(BuyOrderExecutedNotification notification, CancellationToken cancellationToken)
    {
        var priceChanged = await processingService.ApplyForStockAsync(
            notification.Event.StockId,
            MarketPriceInput.Buy(notification.Event.Quantity),
            notification.Event.OccurredAt,
            cancellationToken);

        if (priceChanged is not null)
        {
            await publisher.Publish(new PriceChangedNotification(priceChanged), cancellationToken);
        }
    }
}
