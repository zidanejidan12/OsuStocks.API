namespace OsuStocks.Domain.OsuIntegration.Models;

public sealed record OsuAuthorizationState(string State, string? ReturnUrl);
