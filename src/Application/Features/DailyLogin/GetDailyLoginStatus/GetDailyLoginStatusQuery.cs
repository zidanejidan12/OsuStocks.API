using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.DailyLogin.GetDailyLoginStatus;

public sealed record GetDailyLoginStatusQuery(Guid UserId)
    : IRequest<Result<DailyLoginStatusResponse>>;
