namespace OsuStocks.Application.Features.OsuIntegration.Auth.RefreshToken;

public sealed record RefreshTokenResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt);
