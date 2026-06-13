using OsuStocks.Domain.Missions.Models;

namespace OsuStocks.Domain.Repositories;

/// <summary>Write/read access to mission completions.</summary>
public interface IUserMissionCompletionRepository
{
    /// <summary>Completions for the user across the given period keys.</summary>
    Task<IReadOnlyList<MissionCompletionReadModel>> GetCompletionsAsync(
        Guid userId,
        IReadOnlyCollection<string> periodKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically completes a mission for a period and credits the reward: inserts the completion
    /// row (unique on (user, code, period)), credits the wallet, and writes a <c>MissionReward</c>
    /// ledger entry — all in one transaction. Returns false if already completed (idempotent).
    /// </summary>
    Task<bool> TryCompleteAndRewardAsync(
        Guid userId,
        string missionCode,
        string periodKey,
        long rewardCredits,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);
}
