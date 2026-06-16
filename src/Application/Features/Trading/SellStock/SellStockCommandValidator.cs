using FluentValidation;

namespace OsuStocks.Application.Features.Trading.SellStock;

public sealed class SellStockCommandValidator : AbstractValidator<SellStockCommand>
{
    public SellStockCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.StockId).NotEmpty();
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .Must(quantity => decimal.Round(quantity, 2) == quantity)
            .WithMessage("Quantity supports at most 2 decimal places.");
    }
}
