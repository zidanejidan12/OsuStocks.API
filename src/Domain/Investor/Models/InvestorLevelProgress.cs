namespace OsuStocks.Domain.Investor.Models;

/// <summary>
/// A fully resolved snapshot of an investor's level standing, derived from total XP.
/// </summary>
/// <param name="Level">Current level (minimum 1; may exceed 100 in the soft-capped region).</param>
/// <param name="Title">Cosmetic title for the current level band.</param>
/// <param name="TotalXp">Lifetime XP earned.</param>
/// <param name="XpIntoLevel">XP accumulated since reaching the current level.</param>
/// <param name="XpForNextLevel">XP required to advance from the current level to the next.</param>
/// <param name="ProgressToNext">Fraction (0..1) of progress toward the next level.</param>
public sealed record InvestorLevelProgress(
    int Level,
    string Title,
    long TotalXp,
    long XpIntoLevel,
    long XpForNextLevel,
    double ProgressToNext);
