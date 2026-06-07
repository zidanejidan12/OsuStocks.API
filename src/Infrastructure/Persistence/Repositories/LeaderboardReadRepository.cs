using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Models.Leaderboards;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class LeaderboardReadRepository(AppDbContext dbContext) : ILeaderboardReadRepository
{
    public async Task<IReadOnlyList<LeaderboardEntryReadModel>> GetWealthAsync(
        DateTimeOffset periodStart,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Wealth = wallet balance + market value of all positive holdings. Both the balance and the
        // holdings-value aggregate are set-based correlated subqueries EF can translate, so ordering
        // and paging happen in SQL. PeriodChange = currentWealth - the most recent snapshot wealth at
        // or before periodStart (null when no snapshot exists for the user).
        var rows = await dbContext.Users
            .AsNoTracking()
            .Select(user => new WealthRow
            {
                UserId = user.Id,
                Username = user.Username,
                AvatarUrl = user.AvatarUrl,
                CountryCode = user.CountryCode,
                CurrentWealth =
                    (dbContext.Wallets
                        .Where(w => w.UserId == user.Id)
                        .Select(w => (decimal?)w.Balance)
                        .FirstOrDefault() ?? 0m)
                    + (dbContext.Holdings
                        .Where(h => h.Portfolio.UserId == user.Id && h.Quantity > 0)
                        .Select(h => (decimal?)(h.Quantity * h.Stock.CurrentPrice))
                        .Sum() ?? 0m),
                SnapshotWealth = dbContext.Set<WealthSnapshot>()
                    .Where(s => s.UserId == user.Id && s.CapturedAt <= periodStart)
                    .OrderByDescending(s => s.CapturedAt)
                    .Select(s => (decimal?)s.Wealth)
                    .FirstOrDefault()
            })
            .OrderByDescending(x => x.CurrentWealth)
            .ThenBy(x => x.Username)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows
            .Select((x, i) => new LeaderboardEntryReadModel(
                skip + i + 1,
                x.UserId,
                x.Username,
                x.AvatarUrl,
                x.CountryCode,
                x.CurrentWealth,
                x.SnapshotWealth is null ? null : x.CurrentWealth - x.SnapshotWealth.Value))
            .ToList();
    }

    public async Task<IReadOnlyList<LeaderboardEntryReadModel>> GetProfitAsync(
        DateTimeOffset periodStart,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Profit = Wealth - NetDeposits. NetDeposits = SUM(deposit-type amounts) - SUM(AdminDeduction
        // amounts); amounts are stored positive. Each component is a translatable correlated subquery
        // so ordering/paging stay in SQL. PeriodChange = currentProfit - the most recent snapshot
        // profit at or before periodStart (null when no snapshot exists).
        var rows = await dbContext.Users
            .AsNoTracking()
            .Select(user => new ProfitRow
            {
                UserId = user.Id,
                Username = user.Username,
                AvatarUrl = user.AvatarUrl,
                CountryCode = user.CountryCode,
                CurrentWealth =
                    (dbContext.Wallets
                        .Where(w => w.UserId == user.Id)
                        .Select(w => (decimal?)w.Balance)
                        .FirstOrDefault() ?? 0m)
                    + (dbContext.Holdings
                        .Where(h => h.Portfolio.UserId == user.Id && h.Quantity > 0)
                        .Select(h => (decimal?)(h.Quantity * h.Stock.CurrentPrice))
                        .Sum() ?? 0m),
                Deposits = dbContext.WalletTransactions
                    .Where(t => t.Wallet.UserId == user.Id
                        && (t.TransactionType == WalletTransactionType.InitialGrant
                            || t.TransactionType == WalletTransactionType.AdminGrant
                            || t.TransactionType == WalletTransactionType.DailyReward))
                    .Select(t => (decimal?)t.Amount)
                    .Sum() ?? 0m,
                Deductions = dbContext.WalletTransactions
                    .Where(t => t.Wallet.UserId == user.Id
                        && t.TransactionType == WalletTransactionType.AdminDeduction)
                    .Select(t => (decimal?)t.Amount)
                    .Sum() ?? 0m,
                SnapshotProfit = dbContext.Set<WealthSnapshot>()
                    .Where(s => s.UserId == user.Id && s.CapturedAt <= periodStart)
                    .OrderByDescending(s => s.CapturedAt)
                    .Select(s => (decimal?)s.Profit)
                    .FirstOrDefault()
            })
            .OrderByDescending(x => x.CurrentWealth - (x.Deposits - x.Deductions))
            .ThenBy(x => x.Username)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows
            .Select((x, i) =>
            {
                var profit = x.CurrentWealth - (x.Deposits - x.Deductions);
                return new LeaderboardEntryReadModel(
                    skip + i + 1,
                    x.UserId,
                    x.Username,
                    x.AvatarUrl,
                    x.CountryCode,
                    profit,
                    x.SnapshotProfit is null ? null : profit - x.SnapshotProfit.Value);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<LeaderboardEntryReadModel>> GetTradersAsync(
        DateTimeOffset periodStart,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Ranks users by traded credit volume (SUM of trade.total_amount) within the window. Grouping
        // by user_id and summing translates to a single SQL aggregate; ordering/paging stay in SQL.
        // PeriodChange is null for traders (the value already represents the in-window volume).
        var rows = await dbContext.Trades
            .AsNoTracking()
            .Where(t => t.ExecutedAt >= periodStart)
            .GroupBy(t => t.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Volume = g.Sum(t => t.TotalAmount)
            })
            .Join(
                dbContext.Users.AsNoTracking(),
                row => row.UserId,
                user => user.Id,
                (row, user) => new TraderRow
                {
                    UserId = row.UserId,
                    Username = user.Username,
                    AvatarUrl = user.AvatarUrl,
                    CountryCode = user.CountryCode,
                    Volume = row.Volume
                })
            .OrderByDescending(x => x.Volume)
            .ThenBy(x => x.Username)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows
            .Select((x, i) => new LeaderboardEntryReadModel(
                skip + i + 1,
                x.UserId,
                x.Username,
                x.AvatarUrl,
                x.CountryCode,
                x.Volume,
                null))
            .ToList();
    }

    private sealed class WealthRow
    {
        public Guid UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public string? AvatarUrl { get; init; }
        public string? CountryCode { get; init; }
        public decimal CurrentWealth { get; init; }
        public decimal? SnapshotWealth { get; init; }
    }

    private sealed class ProfitRow
    {
        public Guid UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public string? AvatarUrl { get; init; }
        public string? CountryCode { get; init; }
        public decimal CurrentWealth { get; init; }
        public decimal Deposits { get; init; }
        public decimal Deductions { get; init; }
        public decimal? SnapshotProfit { get; init; }
    }

    private sealed class TraderRow
    {
        public Guid UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public string? AvatarUrl { get; init; }
        public string? CountryCode { get; init; }
        public decimal Volume { get; init; }
    }
}
