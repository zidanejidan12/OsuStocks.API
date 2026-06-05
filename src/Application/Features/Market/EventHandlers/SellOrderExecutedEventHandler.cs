using MediatR;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Application.Features.Market.Services;
using OsuStocks.Domain.Market.Models;

namespace OsuStocks.Application.Features.Market.EventHandlers;

public sealed class SellOrderExecutedEventHandler(
    IMarketEventProcessingService processingService,
    IPublisher publisher)
    : INotificationHandler<SellOrderExecutedNotification>
{
    public async Task Handle(SellOrderExecutedNotification notification, CancellationToken cancellationToken)
    {
        var priceChanged = await processingService.ApplyForStockAsync(
            notification.Event.StockId,
            MarketPriceInput.Sell(notification.Event.Quantity),
            notification.Event.OccurredAt,
            cancellationToken);

        if (priceChanged is not null)
        {
            await publisher.Publish(new PriceChangedNotification(priceChanged), cancellationToken);
        }
    }
}
