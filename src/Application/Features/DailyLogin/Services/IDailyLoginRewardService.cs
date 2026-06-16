using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.DailyLogin.Services;

/// <summary>The outcome of attempting to grant a user's daily-login reward.</summary>
/// <param name="Granted">True when a reward was newly credited by this call.</param>
/// <param name="AlreadyClaimed">True when the reward for the current server day had already been claimed.</param>
/// <param name="Amount">The reward amount (the newly granted amount, or the already-claimed amount).</param>
/// <param name="StreakDay">The 1-based day in the cycle the reward corresponds to.</param>
/// <param name="NewBalance">The wallet balance after the operation.</param>
public sealed record DailyRewardGrantResult(
    bool Granted,
    bool AlreadyClaimed,
    decimal Amount,
    int StreakDay,
    decimal NewBalance);

public interface IDailyLoginRewardService
{
    /// <summary>
    /// Grants the user's daily-login reward for the current server (UTC) day if it has not already been
    /// claimed. Idempotent per user per UTC day: a repeat call on the same day reports
    /// <see cref="DailyRewardGrantResult.AlreadyClaimed"/> without crediting again. Returns a NOT_FOUND
    /// failure when the user or wallet does not exist.
    /// </summary>
    Task<Result<DailyRewardGrantResult>> GrantDailyRewardAsync(Guid userId, CancellationToken cancellationToken = default);
}
