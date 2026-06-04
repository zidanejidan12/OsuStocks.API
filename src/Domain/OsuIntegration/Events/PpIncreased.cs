namespace OsuStocks.Domain.OsuIntegration.Events;

public sealed record PpIncreased(
    Guid TrackedPlayerId,
    decimal PreviousPp,
    decimal CurrentPp,
    DateTimeOffset OccurredAt)
    : OsuDomainEvent("PpIncreased", TrackedPlayerId, OccurredAt);
