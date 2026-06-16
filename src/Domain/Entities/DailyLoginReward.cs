namespace OsuStocks.Domain.Entities;

/// <summary>
/// Append-only ledger of daily-login currency grants. Exactly one row may exist per
/// (<see cref="UserId"/>, <see cref="RewardDate"/>) — enforced by a database unique index — which is
/// the authoritative idempotency guard for the daily reward. The denormalized
/// <see cref="User.DailyRewardStreak"/> / <see cref="User.LastDailyRewardDate"/> fields are a cache
/// derived from this ledger.
/// </summary>
public sealed class DailyLoginReward
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The server (UTC) calendar date the reward was granted for.</summary>
    public DateOnly RewardDate { get; set; }

    /// <summary>Position in the reward cycle this grant represents (1-based).</summary>
    public int StreakDay { get; set; }

    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
