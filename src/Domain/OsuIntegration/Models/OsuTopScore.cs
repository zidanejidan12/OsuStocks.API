namespace OsuStocks.Domain.OsuIntegration.Models;

public sealed record OsuTopScore(
    long Id,
    decimal? Pp,
    string? CoverUrl = null,
    string? Title = null,
    DateTimeOffset? SetAt = null);
