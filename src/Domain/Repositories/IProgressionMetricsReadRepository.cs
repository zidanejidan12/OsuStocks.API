using OsuStocks.Domain.Achievements.Models;
using OsuStocks.Domain.Missions.Models;

namespace OsuStocks.Domain.Repositories;

/// <summary>
/// Derives achievement/mission metric values for a user from existing data (trades, investor
/// profile). Read-only; no counters are stored.
/// </summary>
public interface IProgressionMetricsReadRepository
{
    /// <summary>Lifetime metric values used by achievements.</summary>
    Task<AchievementMetricsSnapshot> GetAchievementMetricsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Metric values for trades within the half-open window [start, end).</summary>
    Task<MissionMetricsSnapshot> GetMissionMetricsAsync(
        Guid userId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);
}
