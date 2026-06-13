using OsuStocks.Domain.Achievements.Models;

namespace OsuStocks.Domain.Achievements.Interfaces;

/// <summary>Static, code-defined catalog of all achievements.</summary>
public interface IAchievementCatalog
{
    IReadOnlyList<AchievementDefinition> All { get; }
}
