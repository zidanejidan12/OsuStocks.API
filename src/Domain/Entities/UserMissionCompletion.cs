namespace OsuStocks.Domain.Entities;

/// <summary>
/// Records that a user completed a catalog mission for a specific period. One row per
/// (user, mission, period); the unique key makes reward grants idempotent. Mission progress
/// within a period is derived on demand from the user's trades in the period window and is not
/// stored here.
/// </summary>
public sealed class UserMissionCompletion
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Stable catalog code, e.g. "daily-trade-3".</summary>
    public string MissionCode { get; set; } = string.Empty;

    /// <summary>Period identifier: daily "yyyy-MM-dd" or weekly ISO "yyyy-'W'ww" (UTC).</summary>
    public string PeriodKey { get; set; } = string.Empty;

    /// <summary>Credits granted on completion (snapshotted from the catalog at completion time).</summary>
    public long RewardCredits { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public User User { get; set; } = null!;
}
