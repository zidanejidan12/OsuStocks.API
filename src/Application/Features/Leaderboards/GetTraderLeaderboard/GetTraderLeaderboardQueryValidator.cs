using FluentValidation;
using OsuStocks.Application.Features.Leaderboards.Common;

namespace OsuStocks.Application.Features.Leaderboards.GetTraderLeaderboard;

public sealed class GetTraderLeaderboardQueryValidator : AbstractValidator<GetTraderLeaderboardQuery>
{
    public GetTraderLeaderboardQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).GreaterThan(0).LessThanOrEqualTo(100);

        RuleFor(x => x.Period)
            .Must(LeaderboardPeriod.IsValid)
            .WithMessage("Period must be one of: daily, weekly, monthly.");
    }
}
