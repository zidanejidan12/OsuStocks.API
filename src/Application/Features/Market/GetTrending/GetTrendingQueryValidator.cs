using FluentValidation;

namespace OsuStocks.Application.Features.Market.GetTrending;

public sealed class GetTrendingQueryValidator : AbstractValidator<GetTrendingQuery>
{
    private static readonly string[] SupportedWindows =
    [
        "1h",
        "24h",
        "7d"
    ];

    public GetTrendingQueryValidator()
    {
        RuleFor(x => x.Window)
            .Must(x => string.IsNullOrWhiteSpace(x) || SupportedWindows.Contains(x.Trim().ToLowerInvariant()))
            .WithMessage("Window must be one of: 1h, 24h, 7d.");

        RuleFor(x => x.Limit).GreaterThan(0).LessThanOrEqualTo(50);
    }
}
