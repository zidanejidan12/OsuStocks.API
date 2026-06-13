using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Investor.Interfaces;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Investor.GetInvestorLevel;

public sealed class GetInvestorLevelQueryHandler(
    IInvestorProfileReadRepository investorProfileReadRepository,
    IInvestorLevelCalculator levelCalculator)
    : IRequestHandler<GetInvestorLevelQuery, Result<GetInvestorLevelResponse>>
{
    public async Task<Result<GetInvestorLevelResponse>> Handle(
        GetInvestorLevelQuery request,
        CancellationToken cancellationToken)
    {
        // No profile yet (never traded) is a valid state: everyone is at least level 1 with 0 XP.
        var totalXp = await investorProfileReadRepository.GetTotalXpByUserIdAsync(
            request.UserId, cancellationToken) ?? 0L;

        var progress = levelCalculator.GetProgress(totalXp);

        return Result.Success(new GetInvestorLevelResponse(
            progress.Level,
            progress.Title,
            progress.TotalXp,
            progress.XpIntoLevel,
            progress.XpForNextLevel,
            progress.ProgressToNext));
    }
}
