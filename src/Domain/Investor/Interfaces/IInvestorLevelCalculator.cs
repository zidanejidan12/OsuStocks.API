using OsuStocks.Domain.Investor.Models;

namespace OsuStocks.Domain.Investor.Interfaces;

/// <summary>
/// Pure, deterministic conversion between lifetime XP and investor level standing.
/// </summary>
public interface IInvestorLevelCalculator
{
    /// <summary>Cumulative XP required to reach (the floor of) the given level. Level 1 = 0.</summary>
    long XpToReachLevel(int level);

    /// <summary>Cosmetic title for the given level.</summary>
    string GetTitle(int level);

    /// <summary>Resolves the full level standing for a lifetime XP total.</summary>
    InvestorLevelProgress GetProgress(long totalXp);
}
