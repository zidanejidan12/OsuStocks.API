using FluentValidation;

namespace OsuStocks.Application.Features.Market.GetMarketStocks;

public sealed class GetMarketStocksQueryValidator : AbstractValidator<GetMarketStocksQuery>
{
    private static readonly string[] SupportedSorts =
    [
        "price_asc",
        "price_desc",
        "name_asc",
        "name_desc",
        "volume_asc",
        "volume_desc",
        "change24h_asc",
        "change24h_desc"
    ];

    public GetMarketStocksQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).GreaterThan(0).LessThanOrEqualTo(100);

        RuleFor(x => x.Sort)
            .Must(x => string.IsNullOrWhiteSpace(x) || SupportedSorts.Contains(x.Trim().ToLowerInvariant()))
            .WithMessage("Sort must be one of the supported sort options.");
    }
}
