using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Models;

namespace OsuStocks.Domain.Repositories;

/// <summary>
/// Cross-user read access to the trade and wallet-ledger tables for the admin transaction monitor.
/// Read-only: filters by user/stock/type/date and pages, returning the matching rows plus the total
/// count for pagination.
/// </summary>
public interface IAdminTransactionReadRepository
{
    Task<(IReadOnlyList<AdminTradeReadModel> Items, int TotalCount)> GetTradesAsync(
        Guid? userId,
        Guid? stockId,
        TradeType? tradeType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AdminWalletTransactionReadModel> Items, int TotalCount)> GetWalletTransactionsAsync(
        Guid? userId,
        WalletTransactionType? transactionType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
