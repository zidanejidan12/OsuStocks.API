using FluentValidation;

namespace OsuStocks.Application.Features.Market.GetStockAnalytics;

public sealed class GetStockAnalyticsQueryValidator : AbstractValidator<GetStockAnalyticsQuery>
{
    public GetStockAnalyticsQueryValidator()
    {
        RuleFor(x => x.StockId).NotEmpty();
    }
}
