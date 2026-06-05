using FluentValidation;

namespace OsuStocks.Application.Features.Trading.GetHoldings;

public sealed class GetHoldingsQueryValidator : AbstractValidator<GetHoldingsQuery>
{
    public GetHoldingsQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
