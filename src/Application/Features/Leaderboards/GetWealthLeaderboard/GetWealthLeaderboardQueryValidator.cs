using FluentValidation;
using OsuStocks.Application.Features.Leaderboards.Common;

namespace OsuStocks.Application.Features.Leaderboards.GetWealthLeaderboard;

public sealed class GetWealthLeaderboardQueryValidator : AbstractValidator<GetWealthLeaderboardQuery>
{
    public GetWealthLeaderboardQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).GreaterThan(0).LessThanOrEqualTo(100);

        RuleFor(x => x.Period)
            .Must(LeaderboardPeriod.IsValid)
            .WithMessage("Period must be one of: daily, weekly, monthly.");
    }
}
