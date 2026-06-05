using FluentValidation;

namespace OsuStocks.Application.Features.Market.GetMarketStockDetails;

public sealed class GetMarketStockDetailsQueryValidator : AbstractValidator<GetMarketStockDetailsQuery>
{
    public GetMarketStockDetailsQueryValidator()
    {
        RuleFor(x => x.StockId).NotEmpty();
    }
}
