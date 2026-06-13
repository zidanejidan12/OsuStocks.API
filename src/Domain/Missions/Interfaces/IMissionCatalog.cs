using OsuStocks.Domain.Missions.Models;

namespace OsuStocks.Domain.Missions.Interfaces;

/// <summary>Static, code-defined catalog of all missions (daily + weekly).</summary>
public interface IMissionCatalog
{
    IReadOnlyList<MissionDefinition> All { get; }
}
