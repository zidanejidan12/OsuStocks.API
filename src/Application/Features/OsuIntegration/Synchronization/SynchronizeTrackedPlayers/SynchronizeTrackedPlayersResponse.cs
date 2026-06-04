namespace OsuStocks.Application.Features.OsuIntegration.Synchronization.SynchronizeTrackedPlayers;

public sealed record SynchronizeTrackedPlayersResponse(
    int TrackedPlayers,
    int SnapshotsCreated,
    int EventsDetected);
