namespace OsuStocks.Application.Features.PlayerRegistry.SearchOsuPlayers;

public sealed record SearchOsuPlayersResponse(
    IReadOnlyList<SearchOsuPlayerItemResponse> Items);

public sealed record SearchOsuPlayerItemResponse(
    long OsuUserId,
    string Username,
    string? AvatarUrl,
    decimal CurrentPp,
    int? GlobalRank,
    bool IsTracked,
    Guid? TrackedPlayerId,
    bool? IsActive);
