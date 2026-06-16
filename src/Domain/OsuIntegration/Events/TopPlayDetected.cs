namespace OsuStocks.Domain.OsuIntegration.Events;

public sealed record TopPlayDetected(
    Guid TrackedPlayerId,
    long? PreviousTopScoreId,
    long NewTopScoreId,
    decimal? NewTopScorePp,
    DateTimeOffset OccurredAt,
    string? CoverUrl = null,
    string? Title = null,
    decimal? PlayerCurrentPp = null)
    : OsuDomainEvent("TopPlayDetected", TrackedPlayerId, OccurredAt);
