using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class WealthSnapshotRepository(AppDbContext dbContext) : IWealthSnapshotRepository
{
    private static readonly WalletTransactionType[] DepositTypes =
    [
        WalletTransactionType.InitialGrant,
        WalletTransactionType.AdminGrant,
        WalletTransactionType.DailyReward
    ];

    public Task AddRangeAsync(IEnumerable<WealthSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        return dbContext.WealthSnapshots.AddRangeAsync(snapshots, cancellationToken);
    }

    public Task<WealthSnapshot?> GetWealthAtOrBeforeAsync(
        Guid userId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        return dbContext.WealthSnapshots
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.CapturedAt <= asOf)
            .OrderByDescending(x => x.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WealthSnapshot>> BuildSnapshotsForAllUsersAsync(
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default)
    {
        // Wallet balance per user (users are 1:1 with wallets; this is the cash component of wealth).
        var balanceByUser = await dbContext.Wallets
            .AsNoTracking()
            .Select(w => new { w.UserId, w.Balance })
            .ToListAsync(cancellationToken);

        // Holdings value per user: SUM(quantity * current_price) over open holdings, grouped by the
        // owning portfolio's user. Aggregated in SQL via GroupBy so only one row per user is returned.
        var holdingsValueByUser = await (
                from holding in dbContext.Holdings.AsNoTracking()
                where holding.Quantity > 0
                join portfolio in dbContext.Portfolios.AsNoTracking()
                    on holding.PortfolioId equals portfolio.Id
                join stock in dbContext.PlayerStocks.AsNoTracking()
                    on holding.StockId equals stock.Id
                group holding.Quantity * stock.CurrentPrice by portfolio.UserId into g
                select new { UserId = g.Key, Value = g.Sum() })
            .ToListAsync(cancellationToken);

        // Deposit transactions (InitialGrant, AdminGrant, DailyReward) summed per user.
        var depositsByUser = await (
                from transaction in dbContext.WalletTransactions.AsNoTracking()
                where DepositTypes.Contains(transaction.TransactionType)
                join wallet in dbContext.Wallets.AsNoTracking()
                    on transaction.WalletId equals wallet.Id
                group transaction.Amount by wallet.UserId into g
                select new { UserId = g.Key, Amount = g.Sum() })
            .ToListAsync(cancellationToken);

        // Admin deductions summed per user (amounts are stored positive; subtracted from deposits).
        var deductionsByUser = await (
                from transaction in dbContext.WalletTransactions.AsNoTracking()
                where transaction.TransactionType == WalletTransactionType.AdminDeduction
                join wallet in dbContext.Wallets.AsNoTracking()
                    on transaction.WalletId equals wallet.Id
                group transaction.Amount by wallet.UserId into g
                select new { UserId = g.Key, Amount = g.Sum() })
            .ToListAsync(cancellationToken);

        var holdingsLookup = holdingsValueByUser.ToDictionary(x => x.UserId, x => x.Value);
        var depositsLookup = depositsByUser.ToDictionary(x => x.UserId, x => x.Amount);
        var deductionsLookup = deductionsByUser.ToDictionary(x => x.UserId, x => x.Amount);

        var snapshots = new List<WealthSnapshot>(balanceByUser.Count);
        foreach (var wallet in balanceByUser)
        {
            var holdingsValue = holdingsLookup.GetValueOrDefault(wallet.UserId);
            var deposits = depositsLookup.GetValueOrDefault(wallet.UserId);
            var deductions = deductionsLookup.GetValueOrDefault(wallet.UserId);

            var wealth = wallet.Balance + holdingsValue;
            var netDeposits = deposits - deductions;

            snapshots.Add(new WealthSnapshot
            {
                Id = Guid.NewGuid(),
                UserId = wallet.UserId,
                CapturedAt = capturedAt,
                Wealth = wealth,
                NetDeposits = netDeposits,
                Profit = wealth - netDeposits
            });
        }

        return snapshots;
    }
}
