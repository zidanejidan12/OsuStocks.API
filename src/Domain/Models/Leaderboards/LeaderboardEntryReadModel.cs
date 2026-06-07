namespace OsuStocks.Domain.Models.Leaderboards;

public sealed record LeaderboardEntryReadModel(
    int Rank,
    Guid UserId,
    string Username,
    string? AvatarUrl,
    string? CountryCode,
    decimal Value,
    decimal? PeriodChange);
