using OsuStocks.Application.Features.Investor.GetInvestorLevel;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.GetCurrentUserProfile;

public sealed record CurrentUserProfileResponse(
    Guid UserId,
    long OsuUserId,
    string Username,
    string? AvatarUrl,
    string? CountryCode,
    string Role,
    GetInvestorLevelResponse InvestorLevel);
