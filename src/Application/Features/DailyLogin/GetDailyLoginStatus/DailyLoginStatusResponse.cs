namespace OsuStocks.Application.Features.DailyLogin.GetDailyLoginStatus;

/// <summary>
/// Current daily-login state for a user.
/// </summary>
/// <param name="Streak">The reward cycle day (1-based, wrapping back to 1 after the final day) that the
/// current day's reward corresponds to: the day already granted when <see cref="ClaimedToday"/> is true,
/// otherwise the day the next claim will grant. Always describes the same day as <see cref="TodayAmount"/>.</param>
/// <param name="ClaimedToday">Whether the reward for the current server day has already been claimed.</param>
/// <param name="TodayAmount">The amount the user would receive by claiming now (or did receive, if already claimed).</param>
/// <param name="Schedule">The full reward cycle, day 1 first.</param>
/// <param name="ServerTimeUtc">The server's current UTC time, so clients can reason about the boundary.</param>
/// <param name="NextResetUtc">The next UTC midnight, when the next reward day becomes claimable.</param>
public sealed record DailyLoginStatusResponse(
    int Streak,
    bool ClaimedToday,
    decimal TodayAmount,
    IReadOnlyList<decimal> Schedule,
    DateTimeOffset ServerTimeUtc,
    DateTimeOffset NextResetUtc);
