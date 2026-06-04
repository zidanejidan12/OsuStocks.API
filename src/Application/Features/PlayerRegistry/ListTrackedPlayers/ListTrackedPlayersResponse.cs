namespace OsuStocks.Application.Features.PlayerRegistry.ListTrackedPlayers;

public sealed record ListTrackedPlayersResponse(
    IReadOnlyList<TrackedPlayerListItemResponse> Items);

public sealed record TrackedPlayerListItemResponse(
    Guid TrackedPlayerId,
    long OsuUserId,
    string Username,
    string TrackingTier,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
