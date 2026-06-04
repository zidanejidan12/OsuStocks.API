namespace OsuStocks.Application.Features.PlayerRegistry.EnableTrackedPlayer;

public sealed record EnableTrackedPlayerResponse(
    Guid TrackedPlayerId,
    bool IsActive);
