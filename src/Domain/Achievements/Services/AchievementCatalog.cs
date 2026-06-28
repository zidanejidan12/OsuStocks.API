using OsuStocks.Domain.Achievements.Interfaces;
using OsuStocks.Domain.Achievements.Models;

namespace OsuStocks.Domain.Achievements.Services;

/// <summary>
/// The fixed achievement catalog. Pure and stateless; tune by editing this list. Codes must be
/// unique and stable (they are persisted on unlock rows).
/// </summary>
public sealed class AchievementCatalog : IAchievementCatalog
{
    private static readonly IReadOnlyList<AchievementDefinition> Definitions = new[]
    {
        // Reward credits deliberately kept small (~1/3 of the original sizes): wealth should come from
        // trading, not from milestone payouts. See [[trading-economy-decisions]] — "if you lose, you lose".
        new AchievementDefinition("first-trade", "First Steps", "Execute your first trade.", "Trading", AchievementMetric.TotalTrades, 1, 300),
        new AchievementDefinition("trades-10", "Getting Active", "Execute 10 trades.", "Trading", AchievementMetric.TotalTrades, 10, 800),
        new AchievementDefinition("trades-100", "Seasoned Trader", "Execute 100 trades.", "Trading", AchievementMetric.TotalTrades, 100, 3_000),
        new AchievementDefinition("volume-100k", "Big Spender", "Trade 100,000 credits of volume.", "Trading", AchievementMetric.TotalVolume, 100_000, 1_500),
        new AchievementDefinition("volume-1m", "High Roller", "Trade 1,000,000 credits of volume.", "Trading", AchievementMetric.TotalVolume, 1_000_000, 8_000),
        new AchievementDefinition("distinct-5", "Diversified", "Buy 5 different stocks.", "Portfolio", AchievementMetric.DistinctStocksTraded, 5, 1_500),
        new AchievementDefinition("distinct-20", "Portfolio Mogul", "Buy 20 different stocks.", "Portfolio", AchievementMetric.DistinctStocksTraded, 20, 5_000),
        new AchievementDefinition("level-10", "Rising Investor", "Reach investor level 10.", "Progression", AchievementMetric.InvestorLevel, 10, 3_000),
        new AchievementDefinition("level-25", "Market Veteran", "Reach investor level 25.", "Progression", AchievementMetric.InvestorLevel, 25, 10_000),
    };

    public IReadOnlyList<AchievementDefinition> All => Definitions;
}
