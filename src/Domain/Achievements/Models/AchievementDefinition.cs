namespace OsuStocks.Domain.Achievements.Models;

/// <summary>
/// A static catalog achievement. Unlocked once when the user's lifetime <see cref="Metric"/>
/// reaches <see cref="Threshold"/>; grants <see cref="RewardCredits"/>. <see cref="Name"/> is the
/// display badge shown for the unlock.
/// </summary>
/// <param name="Code">Stable unique identifier, e.g. "first-trade".</param>
/// <param name="Name">Display name / badge label.</param>
/// <param name="Description">Short description of how to earn it.</param>
/// <param name="Category">Grouping for display, e.g. "Trading".</param>
/// <param name="Metric">The lifetime metric measured.</param>
/// <param name="Threshold">Inclusive value of the metric required to unlock.</param>
/// <param name="RewardCredits">Credits granted on unlock.</param>
public sealed record AchievementDefinition(
    string Code,
    string Name,
    string Description,
    string Category,
    AchievementMetric Metric,
    long Threshold,
    long RewardCredits);
