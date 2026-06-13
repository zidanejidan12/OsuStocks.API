namespace OsuStocks.Application.Features.Investor.GetInvestorLevel;

/// <summary>
/// The caller's investor level standing. Reused both by the dedicated endpoint and embedded
/// in the <c>/me</c> profile payload.
/// </summary>
public sealed record GetInvestorLevelResponse(
    int Level,
    string Title,
    long TotalXp,
    long XpIntoLevel,
    long XpForNextLevel,
    double ProgressToNext);
