namespace OsuStocks.Application.Features.Leaderboards.GetProfitLeaderboard;

public sealed record GetProfitLeaderboardResponse(
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
    decimal? PeriodChange);
