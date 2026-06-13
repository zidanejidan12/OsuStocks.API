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
        new MissionDefinition("daily-trade-3", "Daily Grind", "Execute 3 trades today.", MissionPeriodType.Daily, MissionMetric.TradesInPeriod, 3, 3_000),
        new MissionDefinition("daily-volume-50k", "Daily Volume", "Trade 50,000 credits of volume today.", MissionPeriodType.Daily, MissionMetric.VolumeInPeriod, 50_000, 4_000),
        new MissionDefinition("weekly-trade-25", "Weekly Warrior", "Execute 25 trades this week.", MissionPeriodType.Weekly, MissionMetric.TradesInPeriod, 25, 15_000),
        new MissionDefinition("weekly-distinct-5", "Weekly Variety", "Trade 5 different stocks this week.", MissionPeriodType.Weekly, MissionMetric.DistinctStocksInPeriod, 5, 12_000),
    };

    public IReadOnlyList<MissionDefinition> All => Definitions;
}
