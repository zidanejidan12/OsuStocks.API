using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Application.Features.PlayerRegistry.AddTrackedPlayer;

public sealed record AddTrackedPlayerResponse(
    Guid TrackedPlayerId,
    long OsuUserId,
    string Username,
    TrackingTier TrackingTier,
    bool IsActive,
    string? AvatarUrl,
    Guid StockId);
