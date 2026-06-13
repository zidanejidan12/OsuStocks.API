namespace OsuStocks.Domain.Achievements.Models;

/// <summary>
/// A user's current lifetime values for every achievement metric, derived from existing data.
/// </summary>
public sealed record AchievementMetricsSnapshot(
    long TotalTrades,
    decimal TotalVolume,
    int DistinctStocksTraded,
    int InvestorLevel)
{
    /// <summary>Current value of the given metric as a comparable whole number.</summary>
    public long ValueOf(AchievementMetric metric) => metric switch
    {
        AchievementMetric.TotalTrades => TotalTrades,
        AchievementMetric.TotalVolume => (long)decimal.Floor(TotalVolume),
        AchievementMetric.DistinctStocksTraded => DistinctStocksTraded,
        AchievementMetric.InvestorLevel => InvestorLevel,
        _ => 0L,
    };
}
