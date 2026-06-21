namespace OsuStocks.Domain.OsuIntegration.Models;

public sealed record OsuUserProfile(
    long OsuUserId,
    string Username,
    string? AvatarUrl,
    decimal CurrentPp,
    int? GlobalRank,
    long? TopScoreId,
    decimal? TopScorePp,
    string? CountryCode = null,
    string? TopScoreCoverUrl = null,
    string? TopScoreTitle = null,
    string? ProfileCoverUrl = null);
