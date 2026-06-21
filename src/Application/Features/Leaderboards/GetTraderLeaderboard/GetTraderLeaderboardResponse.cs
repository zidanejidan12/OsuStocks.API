namespace OsuStocks.Application.Features.Leaderboards.GetTraderLeaderboard;

public sealed record GetTraderLeaderboardResponse(
    IReadOnlyList<LeaderboardEntryResponse> Items,
    string Period,
    int Page,
    int PageSize);

public sealed record LeaderboardEntryResponse(
    int Rank,
    Guid UserId,
    string Username,
    string? AvatarUrl,
    string? CountryCode,
    decimal Value,
    decimal? PeriodChange,
    string? EquippedTitle);
