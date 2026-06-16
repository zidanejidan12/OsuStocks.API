using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.DailyLogin.ClaimDailyLoginReward;

public sealed record ClaimDailyLoginRewardCommand(Guid UserId)
    : IRequest<Result<ClaimDailyLoginRewardResponse>>;
