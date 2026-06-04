namespace OsuStocks.Domain.OsuIntegration.Events;

public sealed record PlayerInactive(
    Guid TrackedPlayerId,
    DateTimeOffset OccurredAt)
    : OsuDomainEvent("PlayerInactive", TrackedPlayerId, OccurredAt);
