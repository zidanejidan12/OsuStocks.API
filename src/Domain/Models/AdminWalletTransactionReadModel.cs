using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Domain.Models;

/// <summary>A wallet-ledger row enriched with the owner's identity, for the admin transaction monitor.</summary>
public sealed record AdminWalletTransactionReadModel(
    Guid Id,
    Guid UserId,
    string Username,
    WalletTransactionType TransactionType,
    decimal Amount,
    Guid? ReferenceId,
    DateTimeOffset CreatedAt);
