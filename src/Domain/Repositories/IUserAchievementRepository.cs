using OsuStocks.Domain.Achievements.Models;

namespace OsuStocks.Domain.Repositories;

/// <summary>Write/read access to achievement unlocks.</summary>
public interface IUserAchievementRepository
{
    /// <summary>The achievements the user has already unlocked, with unlock times.</summary>
    Task<IReadOnlyList<AchievementUnlockReadModel>> GetUnlockedAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically unlocks an achievement and credits the reward: inserts the unlock row (unique on
    /// (user, code)), credits the wallet, and writes an <c>AchievementReward</c> ledger entry — all
    /// in one transaction. Returns false if the achievement was already unlocked (idempotent).
    /// </summary>
    Task<bool> TryUnlockAndRewardAsync(
        Guid userId,
        string achievementCode,
        long rewardCredits,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);
}
