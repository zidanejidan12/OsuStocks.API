namespace OsuStocks.Domain.Missions.Models;

/// <summary>A persisted mission completion for a specific period.</summary>
public sealed record MissionCompletionReadModel(
    string MissionCode,
    string PeriodKey,
    DateTimeOffset CompletedAt,
    long RewardCredits);
