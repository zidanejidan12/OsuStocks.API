using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Missions.GetMissions;

public sealed record GetMissionsQuery(Guid UserId)
    : IRequest<Result<GetMissionsResponse>>;
