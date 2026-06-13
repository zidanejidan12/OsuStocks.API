using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Achievements.Models;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Missions.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class ProgressionMetricsReadRepository(AppDbContext dbContext)
    : IProgressionMetricsReadRepository
{
    public async Task<AchievementMetricsSnapshot> GetAchievementMetricsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var userTrades = dbContext.Trades.AsNoTracking().Where(t => t.UserId == userId);

        var totalTrades = await userTrades.LongCountAsync(cancellationToken);

        var totalVolume = await userTrades
            .Select(t => (decimal?)t.TotalAmount)
            .SumAsync(cancellationToken) ?? 0m;

        var distinctStocksTraded = await userTrades
            .Where(t => t.TradeType == TradeType.Buy)
            .Select(t => t.StockId)
            .Distinct()
            .CountAsync(cancellationToken);

        var investorLevel = await dbContext.InvestorProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.Level)
            .FirstOrDefaultAsync(cancellationToken) ?? 1;

        return new AchievementMetricsSnapshot(totalTrades, totalVolume, distinctStocksTraded, investorLevel);
    }

    public async Task<MissionMetricsSnapshot> GetMissionMetricsAsync(
        Guid userId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        // Half-open window [start, end) so a trade at a period boundary counts in exactly one period.
        var periodTrades = dbContext.Trades
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.ExecutedAt >= start && t.ExecutedAt < end);

        var trades = await periodTrades.LongCountAsync(cancellationToken);

        var volume = await periodTrades
            .Select(t => (decimal?)t.TotalAmount)
            .SumAsync(cancellationToken) ?? 0m;

        var distinctStocks = await periodTrades
            .Select(t => t.StockId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new MissionMetricsSnapshot(trades, volume, distinctStocks);
    }
}
