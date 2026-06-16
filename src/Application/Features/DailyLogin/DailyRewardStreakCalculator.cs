namespace OsuStocks.Application.Features.DailyLogin;

/// <summary>
/// Pure, side-effect-free streak math for the daily-login reward. Takes the current day as a parameter so
/// it is deterministic and unit-testable without a clock. The reward amount itself is looked up by the
/// caller as <c>schedule[StreakDay - 1]</c>.
/// </summary>
public static class DailyRewardStreakCalculator
{
    public readonly record struct StreakResult(bool AlreadyClaimed, int StreakDay);

    /// <summary>
    /// Determines the reward day for a claim made on <paramref name="today"/>.
    /// </summary>
    /// <param name="lastRewardDate">The date of the user's most recent claim, or null if they have never claimed.</param>
    /// <param name="previousStreak">The streak day of that most recent claim (ignored when there is no prior claim).</param>
    /// <param name="today">The server (UTC) date the claim is being made for.</param>
    /// <param name="scheduleLength">The number of days in the reward cycle (must be >= 1).</param>
    /// <returns>
    /// <c>AlreadyClaimed</c> is true when a reward was already granted for <paramref name="today"/>.
    /// Otherwise <c>StreakDay</c> is the 1-based day to grant: the previous day + 1 for a consecutive login,
    /// wrapping back to 1 after the final day; or 1 for a first-ever login or a broken streak (a gap of more
    /// than one day). Out-of-range <paramref name="previousStreak"/> values are normalised, never throw, and
    /// never leave the user stuck.
    /// </returns>
    public static StreakResult Compute(DateOnly? lastRewardDate, int previousStreak, DateOnly today, int scheduleLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(scheduleLength, 1);

        if (lastRewardDate == today)
        {
            return new StreakResult(AlreadyClaimed: true, StreakDay: 0);
        }

        if (lastRewardDate == today.AddDays(-1))
        {
            // Consecutive day: advance, wrapping at the end of the cycle. Deriving the wrap from
            // scheduleLength (rather than a hardcoded constant) keeps it correct if the schedule changes and
            // makes any out-of-range previousStreak deterministic.
            var normalized = ((previousStreak % scheduleLength) + scheduleLength) % scheduleLength;
            return new StreakResult(AlreadyClaimed: false, StreakDay: normalized + 1);
        }

        // First-ever claim (lastRewardDate is null) or a broken streak (gap > 1 day) restarts at day 1.
        return new StreakResult(AlreadyClaimed: false, StreakDay: 1);
    }
}
