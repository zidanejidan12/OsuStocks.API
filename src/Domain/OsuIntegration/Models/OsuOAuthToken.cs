namespace OsuStocks.Domain.OsuIntegration.Models;

public sealed record OsuOAuthToken(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt,
    string Scope);
