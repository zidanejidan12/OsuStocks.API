namespace OsuStocks.Domain.Entities;

/// <summary>
/// Records that a user has unlocked a catalog achievement. One row per (user, achievement);
/// achievements are permanent once unlocked. Progress toward locked achievements is derived
/// on demand from existing data (trades, investor level) and is not stored here.
/// </summary>
public sealed class UserAchievement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Stable catalog code, e.g. "first-trade".</summary>
    public string AchievementCode { get; set; } = string.Empty;

    /// <summary>Credits granted on unlock (snapshotted from the catalog at unlock time).</summary>
    public long RewardCredits { get; set; }

    public DateTimeOffset UnlockedAt { get; set; }

    public User User { get; set; } = null!;
}
