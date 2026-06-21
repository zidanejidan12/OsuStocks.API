using FluentValidation;

namespace OsuStocks.Application.Features.Market.GetTradeQuote;

public sealed class GetTradeQuoteValidator : AbstractValidator<GetTradeQuoteQuery>
{
    public GetTradeQuoteValidator()
    {
        RuleFor(x => x.StockId).NotEmpty();
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .Must(quantity => decimal.Round(quantity, 2) == quantity)
            .WithMessage("Quantity supports at most 2 decimal places.");
    }
}
