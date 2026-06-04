using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.GetCurrentUserProfile;

public sealed class GetCurrentUserProfileQueryHandler(IUserRepository userRepository)
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

        return Result.Success(new CurrentUserProfileResponse(
            user.Id,
            user.OsuUserId,
            user.Username,
            user.Role.ToString()));
    }
}
