using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IDailyLoginRewardRepository
{
    /// <summary>Stages a new ledger row for insertion on the next commit.</summary>
    Task AddAsync(DailyLoginReward reward, CancellationToken cancellationToken = default);

    /// <summary>Returns the already-granted reward for the given user and date, if any (read-only).</summary>
    Task<DailyLoginReward?> GetByUserAndDateAsync(Guid userId, DateOnly rewardDate, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's most recent ledger row (highest <see cref="DailyLoginReward.RewardDate"/>), if any.</summary>
    Task<DailyLoginReward?> GetLatestByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current unit of work (the staged ledger row plus any wallet/transaction/user
    /// changes tracked on the same context) atomically. Returns <c>true</c> when the claim committed,
    /// or <c>false</c> when the per-day unique constraint rejected it (the day was already claimed by a
    /// concurrent request); in that case nothing is persisted. Any other failure (including an
    /// optimistic-concurrency conflict on the wallet) propagates to the caller.
    /// </summary>
    Task<bool> TryCommitClaimAsync(CancellationToken cancellationToken = default);
}
