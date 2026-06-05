using OsuStocks.Domain.Market.Events;

namespace OsuStocks.Application.Features.Market.Services;

public interface IMarketEventProcessingService
{
    Task<PriceChanged?> ApplyForStockAsync(
        Guid stockId,
        OsuStocks.Domain.Market.Models.MarketPriceInput input,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);

    Task<PriceChanged?> ApplyForTrackedPlayerAsync(
        Guid trackedPlayerId,
        OsuStocks.Domain.Market.Models.MarketPriceInput input,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);
}
