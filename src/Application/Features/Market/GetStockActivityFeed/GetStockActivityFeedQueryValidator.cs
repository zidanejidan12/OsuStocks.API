using FluentValidation;

namespace OsuStocks.Application.Features.Market.GetStockActivityFeed;

public sealed class GetStockActivityFeedQueryValidator : AbstractValidator<GetStockActivityFeedQuery>
{
    public GetStockActivityFeedQueryValidator()
    {
        RuleFor(x => x.StockId).NotEmpty();
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).GreaterThan(0).LessThanOrEqualTo(100);
    }
}
