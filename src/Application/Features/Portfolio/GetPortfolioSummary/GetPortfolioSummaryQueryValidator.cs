using FluentValidation;

namespace OsuStocks.Application.Features.Portfolio.GetPortfolioSummary;

public sealed class GetPortfolioSummaryQueryValidator : AbstractValidator<GetPortfolioSummaryQuery>
{
    public GetPortfolioSummaryQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
