using FluentValidation;

namespace OsuStocks.Application.Features.Market.GetMarketStockHistory;

public sealed class GetMarketStockHistoryQueryValidator : AbstractValidator<GetMarketStockHistoryQuery>
{
    public GetMarketStockHistoryQueryValidator()
    {
        RuleFor(x => x.StockId).NotEmpty();
    }
}
