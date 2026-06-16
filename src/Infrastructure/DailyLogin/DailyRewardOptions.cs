namespace OsuStocks.Infrastructure.DailyLogin;

public sealed class DailyRewardOptions
{
    public const string SectionName = "DailyReward";

    /// <summary>
    /// Reward amounts for each day of the cycle (day 1 first). Intentionally empty by default: the
    /// configuration binder APPENDS bound entries to a pre-populated collection, so a non-empty default here
    /// would concatenate with any configured values. When this is empty <see cref="DailyRewardSettings"/>
    /// supplies the built-in schedule, so configuration cleanly replaces rather than extends it.
    /// </summary>
    public IList<decimal> DailyAmounts { get; set; } = new List<decimal>();
}
