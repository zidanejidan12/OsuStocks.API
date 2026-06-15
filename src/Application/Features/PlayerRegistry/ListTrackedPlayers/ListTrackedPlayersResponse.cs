namespace OsuStocks.Application.Features.PlayerRegistry.ListTrackedPlayers;

public sealed record ListTrackedPlayersResponse(
    IReadOnlyList<TrackedPlayerListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record TrackedPlayerListItemResponse(
    Guid TrackedPlayerId,
    long OsuUserId,
    string Username,
    string TrackingTier,
    bool IsActive,
    string? AvatarUrl,
    Guid? StockId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
