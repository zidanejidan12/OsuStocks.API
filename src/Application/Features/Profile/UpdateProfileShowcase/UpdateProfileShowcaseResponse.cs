namespace OsuStocks.Application.Features.Profile.UpdateProfileShowcase;

public sealed record UpdateProfileShowcaseResponse(
    string? EquippedTitleCode,
    string? EquippedTitle,
    IReadOnlyList<string> ShowcasedAchievementCodes);
