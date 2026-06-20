using FluentValidation;

namespace OsuStocks.Application.Features.Admin.MarketSettings.UpdateMarketSettings;

public sealed class UpdateMarketSettingsCommandValidator : AbstractValidator<UpdateMarketSettingsCommand>
{
    public UpdateMarketSettingsCommandValidator()
    {
        RuleFor(x => x.PpMultiplier)
            .InclusiveBetween(0m, 10m);

        RuleFor(x => x.TradeMultiplier)
            .InclusiveBetween(0m, 10m);

        RuleFor(x => x.DecayMultiplier)
            .InclusiveBetween(0m, 10m);

        RuleFor(x => x.TradeFeeMultiplier)
            .InclusiveBetween(0m, 10m);

        RuleFor(x => x.Actor)
            .MaximumLength(100);
    }
}
