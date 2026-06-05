using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Market.Events;
using OsuStocks.Domain.Market.Interfaces;
using OsuStocks.Domain.Market.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.Services;

public sealed class MarketEventProcessingService(
    IPlayerStockRepository playerStockRepository,
    IStockPriceHistoryRepository stockPriceHistoryRepository,
    IApplicationDbContext dbContext,
    IMarketCoefficientsProvider coefficientsProvider,
    IMarketPriceEngine marketPriceEngine)
    : IMarketEventProcessingService
{
    public async Task<PriceChanged?> ApplyForStockAsync(
        Guid stockId,
        MarketPriceInput input,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var stock = await playerStockRepository.GetByIdAsync(stockId, cancellationToken);
        if (stock is null)
        {
            return null;
        }

        return await ApplyInternalAsync(stock, input, occurredAt, cancellationToken);
    }

    public async Task<PriceChanged?> ApplyForTrackedPlayerAsync(
        Guid trackedPlayerId,
        MarketPriceInput input,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var stock = await playerStockRepository.GetByTrackedPlayerIdAsync(trackedPlayerId, cancellationToken);
        if (stock is null)
        {
            return null;
        }

        return await ApplyInternalAsync(stock, input, occurredAt, cancellationToken);
    }

    private async Task<PriceChanged> ApplyInternalAsync(
        PlayerStock stock,
        MarketPriceInput input,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var coefficients = await coefficientsProvider.GetCurrentAsync(cancellationToken);
        var calculation = marketPriceEngine.Calculate(stock.CurrentPrice, input, coefficients);
        var reason = ResolveReason(input.Type);

        stock.CurrentPrice = calculation.NewPrice;
        stock.LastUpdated = occurredAt;
        stock.UpdatedAt = occurredAt;
        stock.UpdatedBy = "market-engine";
        playerStockRepository.Update(stock);

        await stockPriceHistoryRepository.AddAsync(new StockPriceHistory
        {
            Id = Guid.NewGuid(),
            StockId = stock.Id,
            PreviousPrice = calculation.PreviousPrice,
            NewPrice = calculation.NewPrice,
            Reason = reason,
            CreatedAt = occurredAt
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PriceChanged(stock.Id, calculation.PreviousPrice, calculation.NewPrice, reason, occurredAt);
    }

    private static PriceChangeReason ResolveReason(MarketInputType type)
    {
        return type switch
        {
            MarketInputType.BuyOrderExecuted => PriceChangeReason.BuyPressure,
            MarketInputType.SellOrderExecuted => PriceChangeReason.SellPressure,
            MarketInputType.TopPlayDetected => PriceChangeReason.TopPlay,
            MarketInputType.PpIncreased => PriceChangeReason.PPGain,
            MarketInputType.PlayerInactive => PriceChangeReason.Decay,
            _ => PriceChangeReason.AdminAdjustment
        };
    }
}

