using FluentValidation;

namespace OsuStocks.Application.Features.Trading.BuyStock;

public sealed class BuyStockCommandValidator : AbstractValidator<BuyStockCommand>
{
    public BuyStockCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.StockId).NotEmpty();
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .Must(quantity => decimal.Round(quantity, 2) == quantity)
            .WithMessage("Quantity supports at most 2 decimal places.");
    }
}
