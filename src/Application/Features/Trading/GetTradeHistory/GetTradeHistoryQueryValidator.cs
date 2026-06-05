using FluentValidation;

namespace OsuStocks.Application.Features.Trading.GetTradeHistory;

public sealed class GetTradeHistoryQueryValidator : AbstractValidator<GetTradeHistoryQuery>
{
    public GetTradeHistoryQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
