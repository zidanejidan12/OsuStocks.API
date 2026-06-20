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
    ITradeFeePolicy tradeFeePolicy,
    ILiquidityProvider liquidityProvider,
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

        // Liquidity (float + recent volume) dampens both the price impact and the spread: deep stocks
        // barely move, thin stocks swing. Move the price + stage it in THIS transaction.
        var liquidity = await liquidityProvider.GetLiquidityAsync(stock.Id, cancellationToken);
        var staged = await marketEventProcessingService.ApplyAndStageAsync(
            stock, MarketPriceInput.Sell(request.Quantity, liquidity), executedAt, cancellationToken);
        var priceChange = staged.PriceChange;

        // Fill at the AVERAGE of the pre/post price (slippage) MINUS half the bid/ask spread on the
        // sell side. Slippage kills the dump-into-your-own-pump round trip; the spread is the liquidity cost.
        var mid = (priceChange.PreviousPrice + priceChange.NewPrice) / 2m;
        var unitPrice = mid * (1m - staged.SpreadRate / 2m);
        var totalAmount = unitPrice * request.Quantity;

        // Progressive service fee, deducted from the proceeds and burned (inflation sink).
        var fee = await tradeFeePolicy.ComputeFeeAsync(totalAmount, cancellationToken);
        var netProceeds = totalAmount - fee;

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

        wallet.Balance += netProceeds;
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

        if (fee > 0m)
        {
            await walletTransactionRepository.AddAsync(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                TransactionType = WalletTransactionType.TradeFee,
                Amount = fee,
                ReferenceId = trade.Id,
                CreatedAt = executedAt
            }, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await publisher.Publish(new SellOrderExecutedNotification(
            new SellOrderExecuted(request.UserId, stock.Id, request.Quantity, unitPrice, executedAt)), cancellationToken);
        await publisher.Publish(new PriceChangedNotification(priceChange), cancellationToken);

        await tradingGuardService.CheckRapidTradingAsync(request.UserId, cancellationToken);

        return Result.Success(new SellStockResponse(trade.Id, trade.Quantity, unitPrice, totalAmount, fee));
    }
}
