namespace OsuStocks.Application.Common.Interfaces;

public interface IDailyRewardSettings
{
    /// <summary>
    /// The reward amounts for each day of the login cycle, day 1 first. The cycle length is the count of
    /// this list: after the final day a consecutive login wraps back to the first amount.
    /// </summary>
    IReadOnlyList<decimal> DailyAmounts { get; }
}
