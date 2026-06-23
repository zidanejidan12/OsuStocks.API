using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class AdminTransactionReadRepository(AppDbContext dbContext) : IAdminTransactionReadRepository
{
    public async Task<(IReadOnlyList<AdminTradeReadModel> Items, int TotalCount)> GetTradesAsync(
        Guid? userId,
        Guid? stockId,
        TradeType? tradeType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Trades.AsNoTracking();

        if (userId.HasValue) query = query.Where(x => x.UserId == userId.Value);
        if (stockId.HasValue) query = query.Where(x => x.StockId == stockId.Value);
        if (tradeType.HasValue) query = query.Where(x => x.TradeType == tradeType.Value);
        if (from.HasValue) query = query.Where(x => x.ExecutedAt >= from.Value);
        // Half-open upper bound so a date filter includes the whole "to" day when the caller passes midnight.
        if (to.HasValue) query = query.Where(x => x.ExecutedAt < to.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.ExecutedAt)
            .Skip(skip)
            .Take(take)
            .Select(x => new AdminTradeReadModel(
                x.Id,
                x.UserId,
                x.User.Username,
                x.User.AvatarUrl,
                x.StockId,
                x.Stock.TrackedPlayer.Username,
                x.TradeType,
                x.Quantity,
                x.UnitPrice,
                x.TotalAmount,
                x.ExecutedAt))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<AdminWalletTransactionReadModel> Items, int TotalCount)> GetWalletTransactionsAsync(
        Guid? userId,
        WalletTransactionType? transactionType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.WalletTransactions.AsNoTracking();

        if (userId.HasValue) query = query.Where(x => x.Wallet.UserId == userId.Value);
        if (transactionType.HasValue) query = query.Where(x => x.TransactionType == transactionType.Value);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(x => new AdminWalletTransactionReadModel(
                x.Id,
                x.Wallet.UserId,
                x.Wallet.User.Username,
                x.TransactionType,
                x.Amount,
                x.ReferenceId,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
