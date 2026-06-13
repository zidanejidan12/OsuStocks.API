using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Achievements.GetAchievements;

public sealed record GetAchievementsQuery(Guid UserId)
    : IRequest<Result<GetAchievementsResponse>>;
