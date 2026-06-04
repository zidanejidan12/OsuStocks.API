namespace OsuStocks.Domain.OsuIntegration.Events;

public abstract record OsuDomainEvent(
    string EventType,
    Guid TrackedPlayerId,
    DateTimeOffset OccurredAt);
