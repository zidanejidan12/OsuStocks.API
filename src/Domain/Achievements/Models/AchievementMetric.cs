namespace OsuStocks.Domain.Achievements.Models;

/// <summary>
/// A lifetime, monotonic user metric an achievement is measured against. All values are derived
/// on demand from existing data, never stored as counters.
/// </summary>
public enum AchievementMetric
{
    /// <summary>Total number of executed trades (buys + sells).</summary>
    TotalTrades = 1,

    /// <summary>Lifetime gross traded credits (sum of trade total amounts).</summary>
    TotalVolume = 2,

    /// <summary>Distinct stocks the user has ever bought.</summary>
    DistinctStocksTraded = 3,

    /// <summary>Current investor level.</summary>
    InvestorLevel = 4,
}
