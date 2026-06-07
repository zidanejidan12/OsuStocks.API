using FluentValidation;

namespace OsuStocks.Application.Features.Market.GetMarketActivityFeed;

public sealed class GetMarketActivityFeedQueryValidator : AbstractValidator<GetMarketActivityFeedQuery>
{
    public GetMarketActivityFeedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).GreaterThan(0).LessThanOrEqualTo(100);
    }
}
