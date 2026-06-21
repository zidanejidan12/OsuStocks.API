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
    /// NOT call SaveChanges — the caller commits it in its own transaction. Returns the staged price
    /// change plus the liquidity-based bid/ask spread, so a trade can atomically move the price and
    /// price its own fill (slippage + spread) in a single save.
    /// </summary>
    Task<StagedPriceResult> ApplyAndStageAsync(
        OsuStocks.Domain.Entities.PlayerStock stock,
        OsuStocks.Domain.Market.Models.MarketPriceInput input,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pure, side-effect-free price calculation for a hypothetical event (no mutation, no staging, no
    /// save). Uses the same engine + live coefficients as <see cref="ApplyAndStageAsync"/>, so a quote
    /// matches what an actual trade would do. Powers the pre-trade cost/fee estimate.
    /// </summary>
    Task<PricePreview> PreviewAsync(
        decimal currentPrice,
        OsuStocks.Domain.Market.Models.MarketPriceInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>The staged (uncommitted) price change plus the liquidity-based spread rate for the trade.</summary>
public sealed record StagedPriceResult(PriceChanged PriceChange, decimal SpreadRate);

/// <summary>A previewed price move (no side effects): the pre/post price and the bid/ask spread rate.</summary>
public sealed record PricePreview(decimal PreviousPrice, decimal NewPrice, decimal SpreadRate);
