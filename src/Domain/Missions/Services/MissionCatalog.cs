using OsuStocks.Domain.Missions.Interfaces;
using OsuStocks.Domain.Missions.Models;

namespace OsuStocks.Domain.Missions.Services;

/// <summary>
/// The fixed mission catalog (daily + weekly). Pure and stateless; tune by editing this list.
/// Codes must be unique and stable (they are persisted on completion rows).
/// </summary>
public sealed class MissionCatalog : IMissionCatalog
{
    private static readonly IReadOnlyList<MissionDefinition> Definitions = new[]
    {
        // Reward credits deliberately kept small (~1/3 of the original sizes) so missions top players up
        // rather than fund a comeback. See [[trading-economy-decisions]] — "if you lose, you lose".
        new MissionDefinition("daily-trade-3", "Daily Grind", "Execute 3 trades today.", MissionPeriodType.Daily, MissionMetric.TradesInPeriod, 3, 1_000),
        new MissionDefinition("daily-volume-50k", "Daily Volume", "Trade 50,000 credits of volume today.", MissionPeriodType.Daily, MissionMetric.VolumeInPeriod, 50_000, 1_500),
        new MissionDefinition("weekly-trade-25", "Weekly Warrior", "Execute 25 trades this week.", MissionPeriodType.Weekly, MissionMetric.TradesInPeriod, 25, 5_000),
        new MissionDefinition("weekly-distinct-5", "Weekly Variety", "Trade 5 different stocks this week.", MissionPeriodType.Weekly, MissionMetric.DistinctStocksInPeriod, 5, 4_000),
    };

    public IReadOnlyList<MissionDefinition> All => Definitions;
}
