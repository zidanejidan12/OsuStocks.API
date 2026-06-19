using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Application.Features.Market.Services;
using OsuStocks.Application.Features.Trading.Services;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Market.Events;
using OsuStocks.Domain.Market.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Trading.SellStock;

public sealed class SellStockCommandHandler(
    IMarketSettingsRepository marketSettingsRepository,
    IWalletRepository walletRepository,
    IPortfolioRepository portfolioRepository,
    IPlayerStockRepository playerStockRepository,
    IHoldingRepository holdingRepository,
    ITradeRepository tradeRepository,
    IWalletTransactionRepository walletTransactionRepository,
    IApplicationDbContext dbContext,
    IPublisher publisher,
    IMarketEventProcessingService marketEventProcessingService,
    ITradingGuardService tradingGuardService)
    : IRequestHandler<SellStockCommand, Result<SellStockResponse>>
{
    public async Task<Result<SellStockResponse>> Handle(SellStockCommand request, CancellationToken cancellationToken)
    {
        var marketSettings = await marketSettingsRepository.GetCurrentAsync(cancellationToken);
        if (marketSettings?.IsMaintenanceMode == true)
        {
            return Result.Failure<SellStockResponse>("CONFLICT", "Market is in maintenance mode.");
        }

        var wallet = await walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet is null)
        {
            return Result.Failure<SellStockResponse>("NOT_FOUND", "Wallet not found.");
        }

        var portfolio = await portfolioRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (portfolio is null)
        {
            return Result.Failure<SellStockResponse>("NOT_FOUND", "Portfolio not found.");
        }

        var stock = await playerStockRepository.GetByIdAsync(request.StockId, cancellationToken);
        if (stock is null)
        {
            return Result.Failure<SellStockResponse>("NOT_FOUND", "Stock not found.");
        }

        var cooldownResult = await tradingGuardService.CheckCooldownAsync(
            request.UserId, request.StockId, cancellationToken);
        if (!cooldownResult.IsSuccess)
        {
            return Result.Failure<SellStockResponse>(cooldownResult.Error!.Code, cooldownResult.Error.Message);
        }

        var holding = await holdingRepository.GetByPortfolioAndStockAsync(portfolio.Id, stock.Id, cancellationToken);
        if (holding is null)
        {
            return Result.Failure<SellStockResponse>("INSUFFICIENT_HOLDINGS", "Holding not found for stock.");
        }

        if (holding.Quantity < request.Quantity)
        {
            return Result.Failure<SellStockResponse>("INSUFFICIENT_HOLDINGS", "Holding quantity is insufficient.");
        }

        var executedAt = DateTimeOffset.UtcNow;

        // Move the price for this order and stage the change in THIS transaction. The seller receives
        // the AVERAGE of the pre- and post-trade price (slippage), so a large dump can't be sold in
        // full at the high pre-dump price — closing the pump-and-dump loophole. Per-order impact is
        // capped in the engine, and the price floor still applies.
        var priceChange = await marketEventProcessingService.ApplyAndStageAsync(
            stock, MarketPriceInput.Sell(request.Quantity), executedAt, cancellationToken);
        var unitPrice = (priceChange.PreviousPrice + priceChange.NewPrice) / 2m;
        var totalAmount = unitPrice * request.Quantity;

        holding.Quantity -= request.Quantity;
        holding.UpdatedAt = DateTimeOffset.UtcNow;
        holding.UpdatedBy = "trading";

        if (holding.Quantity == 0)
        {
            holdingRepository.Remove(holding);
        }
        else
        {
            holdingRepository.Update(holding);
        }

        wallet.Balance += totalAmount;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;
        wallet.UpdatedBy = "trading";
        walletRepository.Update(wallet);

        var trade = new Trade
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            StockId = stock.Id,
            TradeType = TradeType.Sell,
            Quantity = request.Quantity,
            UnitPrice = unitPrice,
            TotalAmount = totalAmount,
            ExecutedAt = executedAt
        };
        await tradeRepository.AddAsync(trade, cancellationToken);

        var walletTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionType = WalletTransactionType.SellStock,
            Amount = totalAmount,
            ReferenceId = trade.Id,
            CreatedAt = executedAt
        };
        await walletTransactionRepository.AddAsync(walletTransaction, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await publisher.Publish(new SellOrderExecutedNotification(
            new SellOrderExecuted(request.UserId, stock.Id, request.Quantity, unitPrice, executedAt)), cancellationToken);
        await publisher.Publish(new PriceChangedNotification(priceChange), cancellationToken);

        await tradingGuardService.CheckRapidTradingAsync(request.UserId, cancellationToken);

        return Result.Success(new SellStockResponse(trade.Id, unitPrice, totalAmount));
    }
}
