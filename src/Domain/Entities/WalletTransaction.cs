using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Domain.Entities;

public sealed class WalletTransaction
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public WalletTransactionType TransactionType { get; set; }
    public decimal Amount { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Wallet Wallet { get; set; } = null!;
}
