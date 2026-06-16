using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Application.Features.DailyLogin.Services;

namespace OsuStocks.Application.Features.DailyLogin.ClaimDailyLoginReward;

public sealed class ClaimDailyLoginRewardCommandHandler(IDailyLoginRewardService dailyLoginRewardService)
    : IRequestHandler<ClaimDailyLoginRewardCommand, Result<ClaimDailyLoginRewardResponse>>
{
    public async Task<Result<ClaimDailyLoginRewardResponse>> Handle(
        ClaimDailyLoginRewardCommand request,
        CancellationToken cancellationToken)
    {
        var result = await dailyLoginRewardService.GrantDailyRewardAsync(request.UserId, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return Result.Failure<ClaimDailyLoginRewardResponse>(result.Error!.Code, result.Error.Message);
        }

        var grant = result.Value;

        // Already-claimed is a successful, idempotent outcome (granted = false), not an error.
        return Result.Success(new ClaimDailyLoginRewardResponse(
            grant.Granted,
            grant.AlreadyClaimed,
            grant.Amount,
            grant.StreakDay,
            grant.NewBalance));
    }
}
