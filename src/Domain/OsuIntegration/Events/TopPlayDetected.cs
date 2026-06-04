namespace OsuStocks.Domain.OsuIntegration.Events;

public sealed record TopPlayDetected(
    Guid TrackedPlayerId,
    long? PreviousTopScoreId,
    long NewTopScoreId,
    decimal? NewTopScorePp,
    DateTimeOffset OccurredAt)
    : OsuDomainEvent("TopPlayDetected", TrackedPlayerId, OccurredAt);
