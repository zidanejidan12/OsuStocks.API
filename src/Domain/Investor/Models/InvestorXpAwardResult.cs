namespace OsuStocks.Domain.Investor.Models;

/// <summary>
/// Outcome of awarding XP to a user's investor profile.
/// </summary>
/// <param name="Awarded">False when the award was skipped (e.g. non-positive XP or could not be persisted).</param>
/// <param name="PreviousLevel">The user's level before this award.</param>
/// <param name="NewLevel">The user's level after this award.</param>
/// <param name="TotalXp">The user's lifetime XP after this award.</param>
public sealed record InvestorXpAwardResult(
    bool Awarded,
    int PreviousLevel,
    int NewLevel,
    long TotalXp)
{
    public static readonly InvestorXpAwardResult Skipped = new(false, 0, 0, 0L);

    /// <summary>True when this award advanced the user to a higher level.</summary>
    public bool LeveledUp => Awarded && NewLevel > PreviousLevel;
}
