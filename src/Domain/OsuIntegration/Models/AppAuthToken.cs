namespace OsuStocks.Domain.OsuIntegration.Models;

public sealed record AppAuthToken(
    string AccessToken,
    DateTimeOffset ExpiresAt);
