using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.OsuIntegration.Auth.GetCurrentUserProfile;

public sealed record GetCurrentUserProfileQuery(Guid UserId)
    : IRequest<Result<CurrentUserProfileResponse>>;
