namespace OsuStocks.Application.Features.OsuIntegration.Auth.GetCurrentUserProfile;

public sealed record CurrentUserProfileResponse(
    Guid UserId,
    long OsuUserId,
    string Username,
    string? AvatarUrl,
    string? CountryCode,
    string Role);
