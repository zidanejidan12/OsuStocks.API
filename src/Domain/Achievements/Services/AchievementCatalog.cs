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
        new AchievementDefinition("first-trade", "First Steps", "Execute your first trade.", "Trading", AchievementMetric.TotalTrades, 1, 1_000),
        new AchievementDefinition("trades-10", "Getting Active", "Execute 10 trades.", "Trading", AchievementMetric.TotalTrades, 10, 2_500),
        new AchievementDefinition("trades-100", "Seasoned Trader", "Execute 100 trades.", "Trading", AchievementMetric.TotalTrades, 100, 10_000),
        new AchievementDefinition("volume-100k", "Big Spender", "Trade 100,000 credits of volume.", "Trading", AchievementMetric.TotalVolume, 100_000, 5_000),
        new AchievementDefinition("volume-1m", "High Roller", "Trade 1,000,000 credits of volume.", "Trading", AchievementMetric.TotalVolume, 1_000_000, 25_000),
        new AchievementDefinition("distinct-5", "Diversified", "Buy 5 different stocks.", "Portfolio", AchievementMetric.DistinctStocksTraded, 5, 5_000),
        new AchievementDefinition("distinct-20", "Portfolio Mogul", "Buy 20 different stocks.", "Portfolio", AchievementMetric.DistinctStocksTraded, 20, 15_000),
        new AchievementDefinition("level-10", "Rising Investor", "Reach investor level 10.", "Progression", AchievementMetric.InvestorLevel, 10, 10_000),
        new AchievementDefinition("level-25", "Market Veteran", "Reach investor level 25.", "Progression", AchievementMetric.InvestorLevel, 25, 30_000),
    };

    public IReadOnlyList<AchievementDefinition> All => Definitions;
}
