namespace OsuStocks.Application.Features.Admin.TransactionMonitor.GetAdminWalletTransactions;

public sealed record GetAdminWalletTransactionsResponse(
    IReadOnlyList<AdminWalletTransactionItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminWalletTransactionItemResponse(
    Guid Id,
    Guid UserId,
    string Username,
    string TransactionType,
    decimal Amount,
    Guid? ReferenceId,
    DateTimeOffset CreatedAt);
