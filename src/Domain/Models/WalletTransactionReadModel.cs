namespace OsuStocks.Domain.Models;

public sealed record WalletTransactionReadModel(
    Guid Id,
    string TransactionType,
    decimal Amount,
    Guid? ReferenceId,
    DateTimeOffset CreatedAt);
