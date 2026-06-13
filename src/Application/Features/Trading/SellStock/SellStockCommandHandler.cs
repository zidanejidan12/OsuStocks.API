using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Application.Features.Trading.Services;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Market.Events;
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

        var unitPrice = stock.CurrentPrice;
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
            ExecutedAt = DateTimeOffset.UtcNow
        };
        await tradeRepository.AddAsync(trade, cancellationToken);

        var walletTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionType = WalletTransactionType.SellStock,
            Amount = totalAmount,
            ReferenceId = trade.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await walletTransactionRepository.AddAsync(walletTransaction, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await publisher.Publish(new SellOrderExecutedNotification(
            new SellOrderExecuted(request.UserId, stock.Id, request.Quantity, unitPrice, DateTimeOffset.UtcNow)), cancellationToken);

        await tradingGuardService.CheckRapidTradingAsync(request.UserId, cancellationToken);

        return Result.Success(new SellStockResponse(trade.Id, unitPrice, totalAmount));
    }
}
