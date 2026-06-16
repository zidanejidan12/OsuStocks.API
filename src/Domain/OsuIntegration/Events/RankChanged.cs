namespace OsuStocks.Domain.OsuIntegration.Events;

public sealed record RankChanged(
    Guid TrackedPlayerId,
    int PreviousRank,
    int CurrentRank,
    DateTimeOffset OccurredAt)
    : OsuDomainEvent("RankChanged", TrackedPlayerId, OccurredAt);
