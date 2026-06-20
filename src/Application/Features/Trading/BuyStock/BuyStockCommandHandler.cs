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

namespace OsuStocks.Application.Features.Trading.BuyStock;

public sealed class BuyStockCommandHandler(
    IMarketSettingsRepository marketSettingsRepository,
    IWalletRepository walletRepository,
    IPortfolioRepository portfolioRepository,
    IPlayerStockRepository playerStockRepository,
    ITrackedPlayerRepository trackedPlayerRepository,
    IHoldingRepository holdingRepository,
    ITradeRepository tradeRepository,
    IWalletTransactionRepository walletTransactionRepository,
    IApplicationDbContext dbContext,
    IPublisher publisher,
    IMarketEventProcessingService marketEventProcessingService,
    ITradeFeePolicy tradeFeePolicy,
    ILiquidityProvider liquidityProvider,
    ITradingGuardService tradingGuardService)
    : IRequestHandler<BuyStockCommand, Result<BuyStockResponse>>
{
    public async Task<Result<BuyStockResponse>> Handle(BuyStockCommand request, CancellationToken cancellationToken)
    {
        var marketSettings = await marketSettingsRepository.GetCurrentAsync(cancellationToken);
        if (marketSettings?.IsMaintenanceMode == true)
        {
            return Result.Failure<BuyStockResponse>("CONFLICT", "Market is in maintenance mode.");
        }

        var wallet = await walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet is null)
        {
            return Result.Failure<BuyStockResponse>("NOT_FOUND", "Wallet not found.");
        }

        var portfolio = await portfolioRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (portfolio is null)
        {
            return Result.Failure<BuyStockResponse>("NOT_FOUND", "Portfolio not found.");
        }

        var stock = await playerStockRepository.GetByIdAsync(request.StockId, cancellationToken);
        if (stock is null)
        {
            return Result.Failure<BuyStockResponse>("NOT_FOUND", "Stock not found.");
        }

        var trackedPlayer = await trackedPlayerRepository.GetByIdAsync(stock.TrackedPlayerId, cancellationToken);
        if (trackedPlayer is null)
        {
            return Result.Failure<BuyStockResponse>("NOT_FOUND", "Tracked player not found.");
        }

        if (!trackedPlayer.IsActive)
        {
            return Result.Failure<BuyStockResponse>("CONFLICT", "Stock is disabled for buying.");
        }

        var cooldownResult = await tradingGuardService.CheckCooldownAsync(
            request.UserId, request.StockId, cancellationToken);
        if (!cooldownResult.IsSuccess)
        {
            return Result.Failure<BuyStockResponse>(cooldownResult.Error!.Code, cooldownResult.Error.Message);
        }

        var existingHoldingForLimit = await holdingRepository.GetByPortfolioAndStockAsync(
            portfolio.Id, stock.Id, cancellationToken);
        var currentQuantity = existingHoldingForLimit?.Quantity ?? 0;

        var positionResult = await tradingGuardService.CheckPositionLimitAsync(
            request.UserId, request.StockId, request.Quantity, currentQuantity, cancellationToken);
        if (!positionResult.IsSuccess)
        {
            return Result.Failure<BuyStockResponse>(positionResult.Error!.Code, positionResult.Error.Message);
        }

        var executedAt = DateTimeOffset.UtcNow;

        // Liquidity (float + recent volume) dampens both the price impact and the spread: deep stocks
        // barely move, thin stocks swing. Move the price + stage it in THIS transaction.
        var liquidity = await liquidityProvider.GetLiquidityAsync(stock.Id, cancellationToken);
        var staged = await marketEventProcessingService.ApplyAndStageAsync(
            stock, MarketPriceInput.Buy(request.Quantity, liquidity), executedAt, cancellationToken);
        var priceChange = staged.PriceChange;

        // Fill at the AVERAGE of the pre/post price (slippage) plus half the bid/ask spread on the buy
        // side. Slippage kills the pump-and-dump round trip; the spread is the liquidity cost of trading.
        var mid = (priceChange.PreviousPrice + priceChange.NewPrice) / 2m;
        var unitPrice = mid * (1m + staged.SpreadRate / 2m);
        var totalAmount = unitPrice * request.Quantity;

        // Progressive service fee, charged on top of the purchase and burned (inflation sink).
        var fee = await tradeFeePolicy.ComputeFeeAsync(totalAmount, cancellationToken);
        var totalDebit = totalAmount + fee;

        if (wallet.Balance < totalDebit)
        {
            // Nothing has been saved yet, so the staged price move is discarded with the scope.
            return Result.Failure<BuyStockResponse>("INSUFFICIENT_BALANCE", "Wallet balance is insufficient.");
        }

        wallet.Balance -= totalDebit;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;
        wallet.UpdatedBy = "trading";
        walletRepository.Update(wallet);

        var existingHolding = existingHoldingForLimit;

        if (existingHolding is null)
        {
            existingHolding = new Holding
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolio.Id,
                StockId = stock.Id,
                Quantity = request.Quantity,
                AveragePrice = unitPrice,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "trading"
            };

            await holdingRepository.AddAsync(existingHolding, cancellationToken);
        }
        else
        {
            var previousQuantity = existingHolding.Quantity;
            var newQuantity = previousQuantity + request.Quantity;
            var weightedTotalCost = (existingHolding.AveragePrice * previousQuantity) + totalAmount;

            existingHolding.Quantity = newQuantity;
            existingHolding.AveragePrice = newQuantity == 0 ? unitPrice : weightedTotalCost / newQuantity;
            existingHolding.UpdatedAt = DateTimeOffset.UtcNow;
            existingHolding.UpdatedBy = "trading";

            holdingRepository.Update(existingHolding);
        }

        var trade = new Trade
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            StockId = stock.Id,
            TradeType = TradeType.Buy,
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
            TransactionType = WalletTransactionType.BuyStock,
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

        await publisher.Publish(new BuyOrderExecutedNotification(
            new BuyOrderExecuted(request.UserId, stock.Id, request.Quantity, unitPrice, executedAt)), cancellationToken);
        await publisher.Publish(new PriceChangedNotification(priceChange), cancellationToken);

        await tradingGuardService.CheckRapidTradingAsync(request.UserId, cancellationToken);

        return Result.Success(new BuyStockResponse(trade.Id, unitPrice, totalAmount, fee));
    }
}
