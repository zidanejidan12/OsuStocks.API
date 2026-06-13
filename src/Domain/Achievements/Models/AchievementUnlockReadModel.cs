namespace OsuStocks.Domain.Achievements.Models;

/// <summary>A persisted achievement unlock.</summary>
public sealed record AchievementUnlockReadModel(
    string AchievementCode,
    DateTimeOffset UnlockedAt);
