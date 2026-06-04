namespace OsuStocks.Domain.OsuIntegration.Models;

public sealed record PlayerSynchronizationSummary(
    int TrackedPlayers,
    int SnapshotsCreated,
    int EventsDetected);
