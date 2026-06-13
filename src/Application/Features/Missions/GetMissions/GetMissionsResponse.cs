namespace OsuStocks.Application.Features.Missions.GetMissions;

public sealed record GetMissionsResponse(
    IReadOnlyList<MissionItemResponse> Items);

public sealed record MissionItemResponse(
    string Code,
    string Name,
    string Description,
    string Period,
    string PeriodKey,
    string Metric,
    long Target,
    long CurrentValue,
    long RewardCredits,
    bool Completed,
    DateTimeOffset? CompletedAt,
    DateTimeOffset ResetsAt);
