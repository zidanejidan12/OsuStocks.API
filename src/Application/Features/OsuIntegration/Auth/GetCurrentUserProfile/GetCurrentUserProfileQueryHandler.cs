using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Application.Features.Investor.GetInvestorLevel;
using OsuStocks.Domain.Investor.Interfaces;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.GetCurrentUserProfile;

public sealed class GetCurrentUserProfileQueryHandler(
    IUserRepository userRepository,
    IInvestorProfileReadRepository investorProfileReadRepository,
    IInvestorLevelCalculator levelCalculator)
    : IRequestHandler<GetCurrentUserProfileQuery, Result<CurrentUserProfileResponse>>
{
    public async Task<Result<CurrentUserProfileResponse>> Handle(
        GetCurrentUserProfileQuery request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<CurrentUserProfileResponse>("NOT_FOUND", "User not found.");
        }

        var totalXp = await investorProfileReadRepository.GetTotalXpByUserIdAsync(
            request.UserId, cancellationToken) ?? 0L;
        var progress = levelCalculator.GetProgress(totalXp);

        return Result.Success(new CurrentUserProfileResponse(
            user.Id,
            user.OsuUserId,
            user.Username,
            user.AvatarUrl,
            user.CountryCode,
            user.ProfileCoverUrl,
            user.Role.ToString(),
            new GetInvestorLevelResponse(
                progress.Level,
                progress.Title,
                progress.TotalXp,
                progress.XpIntoLevel,
                progress.XpForNextLevel,
                progress.ProgressToNext)));
    }
}
