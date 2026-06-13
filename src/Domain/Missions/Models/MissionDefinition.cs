namespace OsuStocks.Domain.Missions.Models;

/// <summary>
/// A static catalog mission. Completed once per period when the user's in-period
/// <see cref="Metric"/> reaches <see cref="Target"/>; grants <see cref="RewardCredits"/>.
/// </summary>
/// <param name="Code">Stable unique identifier, e.g. "daily-trade-3".</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Short description of the goal.</param>
/// <param name="Period">Daily or weekly cadence.</param>
/// <param name="Metric">The per-period metric measured.</param>
/// <param name="Target">Inclusive value of the metric required to complete.</param>
/// <param name="RewardCredits">Credits granted on completion.</param>
public sealed record MissionDefinition(
    string Code,
    string Name,
    string Description,
    MissionPeriodType Period,
    MissionMetric Metric,
    long Target,
    long RewardCredits);
