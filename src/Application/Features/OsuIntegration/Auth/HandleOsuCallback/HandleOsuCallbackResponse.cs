namespace OsuStocks.Application.Features.OsuIntegration.Auth.HandleOsuCallback;

public sealed record HandleOsuCallbackResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string? ReturnUrl);
