namespace OsuStocks.Application.Features.PlayerRegistry.DisableTrackedPlayer;

public sealed record DisableTrackedPlayerResponse(
    Guid TrackedPlayerId,
    bool IsActive);
