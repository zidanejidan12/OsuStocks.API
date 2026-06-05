namespace OsuStocks.Application.Features.Wallet.GetWalletTransactions;

public sealed record GetWalletTransactionsResponse(IReadOnlyList<WalletTransactionItemResponse> Items);

public sealed record WalletTransactionItemResponse(
    Guid TransactionId,
    string TransactionType,
    decimal Amount,
    Guid? ReferenceId,
    DateTimeOffset CreatedAt);
