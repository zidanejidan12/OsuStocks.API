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

    /// <summary>
    /// Computes and applies the price move to an already-loaded, tracked
    /// <see cref="OsuStocks.Domain.Entities.PlayerStock"/> and stages the price-history row, but does
    /// NOT call SaveChanges — the caller commits it in its own transaction. Lets a trade atomically
    /// move the price and price its own fill (slippage) in a single save.
    /// </summary>
    Task<PriceChanged> ApplyAndStageAsync(
        OsuStocks.Domain.Entities.PlayerStock stock,
        OsuStocks.Domain.Market.Models.MarketPriceInput input,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);
}
