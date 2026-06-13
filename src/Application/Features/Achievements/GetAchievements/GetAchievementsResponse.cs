namespace OsuStocks.Application.Features.Achievements.GetAchievements;

public sealed record GetAchievementsResponse(
    int UnlockedCount,
    int TotalCount,
    IReadOnlyList<AchievementItemResponse> Items);

public sealed record AchievementItemResponse(
    string Code,
    string Name,
    string Description,
    string Category,
    string Metric,
    long Threshold,
    long CurrentValue,
    long RewardCredits,
    bool Unlocked,
    DateTimeOffset? UnlockedAt);
