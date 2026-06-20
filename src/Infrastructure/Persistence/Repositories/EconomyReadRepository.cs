using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class EconomyReadRepository(AppDbContext dbContext) : IEconomyReadRepository
{
    // Faucets: ledger types that put new credits into circulation.
    private static readonly WalletTransactionType[] MintedTypes =
    [
        WalletTransactionType.InitialGrant,
        WalletTransactionType.DailyReward,
        WalletTransactionType.AchievementReward,
        WalletTransactionType.MissionReward,
        WalletTransactionType.AdminGrant,
    ];

    // Sinks: ledger types that remove credits from circulation (burned).
    private static readonly WalletTransactionType[] BurnedTypes =
    [
        WalletTransactionType.TradeFee,
        WalletTransactionType.AdminDeduction,
    ];

    public async Task<EconomySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var circulating = await dbContext.Wallets
            .AsNoTracking()
            .Select(w => (decimal?)w.Balance)
            .SumAsync(cancellationToken) ?? 0m;

        var minted = await dbContext.WalletTransactions
            .AsNoTracking()
            .Where(t => MintedTypes.Contains(t.TransactionType))
            .Select(t => (decimal?)t.Amount)
            .SumAsync(cancellationToken) ?? 0m;

        var burned = await dbContext.WalletTransactions
            .AsNoTracking()
            .Where(t => BurnedTypes.Contains(t.TransactionType))
            .Select(t => (decimal?)t.Amount)
            .SumAsync(cancellationToken) ?? 0m;

        return new EconomySnapshot(circulating, minted, burned);
    }
}
