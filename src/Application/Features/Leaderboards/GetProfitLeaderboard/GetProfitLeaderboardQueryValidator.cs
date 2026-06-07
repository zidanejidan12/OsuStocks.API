using FluentValidation;
using OsuStocks.Application.Features.Leaderboards.Common;

namespace OsuStocks.Application.Features.Leaderboards.GetProfitLeaderboard;

public sealed class GetProfitLeaderboardQueryValidator : AbstractValidator<GetProfitLeaderboardQuery>
{
    public GetProfitLeaderboardQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).GreaterThan(0).LessThanOrEqualTo(100);

        RuleFor(x => x.Period)
            .Must(LeaderboardPeriod.IsValid)
            .WithMessage("Period must be one of: daily, weekly, monthly.");
    }
}
